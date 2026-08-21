/*
 * Minimal, dependency-free, XSS-safe Markdown renderer for assistant messages.
 *
 * Supported: fenced code blocks (```lang), inline code (`x`), headings (###),
 * ordered/unordered lists, links [text](url), bold (**x**), paragraphs.
 *
 * Security model: all raw text is HTML-escaped FIRST. Formatting is then applied
 * by inserting known-safe tags. Link URLs are restricted to http(s)/relative.
 * No raw HTML from the source is ever emitted.
 */
(function (global) {
    'use strict';

    function escapeHtml(text) {
        return text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function safeUrl(url) {
        var trimmed = url.trim();
        // Allow http(s), protocol-relative, root-relative, and simple relative links.
        if (/^(https?:\/\/|\/\/|\/|\.{0,2}\/)/i.test(trimmed) || /^[\w.-]+(\/|$)/.test(trimmed)) {
            return trimmed;
        }
        return '#';
    }

    // Inline formatting applied to already-escaped text.
    function renderInline(escaped) {
        var out = escaped;

        // Inline code: `code` (protect content from further formatting via placeholder-free order)
        out = out.replace(/`([^`]+)`/g, function (_, code) {
            return '<code class="inline">' + code + '</code>';
        });

        // Bold: **text**
        out = out.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

        // Links: [text](url)
        out = out.replace(/\[([^\]]+)\]\(([^)]+)\)/g, function (_, text, url) {
            return '<a href="' + escapeHtml(safeUrl(url)) +
                '" target="_blank" rel="noopener noreferrer">' + text + '</a>';
        });

        return out;
    }

    function renderCodeBlock(lang, code) {
        var langLabel = lang ? escapeHtml(lang) : 'code';
        var escapedCode = escapeHtml(code.replace(/\n$/, ''));
        return '' +
            '<div class="code-block">' +
                '<div class="code-block__head">' +
                    '<span>' + langLabel + '</span>' +
                    '<button class="code-block__copy" type="button" data-copy>کپی</button>' +
                '</div>' +
                '<pre><code>' + escapedCode + '</code></pre>' +
            '</div>';
    }

    function render(markdown) {
        if (!markdown) return '';

        var normalized = String(markdown).replace(/\r\n/g, '\n');
        var html = [];
        var lines = normalized.split('\n');
        var i = 0;

        var listBuffer = [];
        var listType = null; // 'ul' | 'ol'

        function flushList() {
            if (!listType) return;
            html.push('<' + listType + '>' + listBuffer.join('') + '</' + listType + '>');
            listBuffer = [];
            listType = null;
        }

        while (i < lines.length) {
            var line = lines[i];

            // Fenced code block
            var fence = line.match(/^\s*```(\w+)?\s*$/);
            if (fence) {
                flushList();
                var lang = fence[1] || '';
                var codeLines = [];
                i++;
                while (i < lines.length && !/^\s*```\s*$/.test(lines[i])) {
                    codeLines.push(lines[i]);
                    i++;
                }
                i++; // skip closing fence
                html.push(renderCodeBlock(lang, codeLines.join('\n')));
                continue;
            }

            // Heading (###, ##, #)
            var heading = line.match(/^\s*(#{1,4})\s+(.*)$/);
            if (heading) {
                flushList();
                html.push('<h3>' + renderInline(escapeHtml(heading[2])) + '</h3>');
                i++;
                continue;
            }

            // Ordered list item
            var ol = line.match(/^\s*\d+[.)]\s+(.*)$/);
            if (ol) {
                if (listType && listType !== 'ol') flushList();
                listType = 'ol';
                listBuffer.push('<li>' + renderInline(escapeHtml(ol[1])) + '</li>');
                i++;
                continue;
            }

            // Unordered list item
            var ul = line.match(/^\s*[-*+]\s+(.*)$/);
            if (ul) {
                if (listType && listType !== 'ul') flushList();
                listType = 'ul';
                listBuffer.push('<li>' + renderInline(escapeHtml(ul[1])) + '</li>');
                i++;
                continue;
            }

            // Blank line
            if (line.trim() === '') {
                flushList();
                i++;
                continue;
            }

            // Paragraph: collect consecutive non-special lines
            flushList();
            var paraLines = [line];
            i++;
            while (i < lines.length &&
                   lines[i].trim() !== '' &&
                   !/^\s*```/.test(lines[i]) &&
                   !/^\s*#{1,4}\s+/.test(lines[i]) &&
                   !/^\s*\d+[.)]\s+/.test(lines[i]) &&
                   !/^\s*[-*+]\s+/.test(lines[i])) {
                paraLines.push(lines[i]);
                i++;
            }
            var paraText = paraLines.map(function (l) {
                return renderInline(escapeHtml(l));
            }).join('<br>');
            html.push('<p>' + paraText + '</p>');
        }

        flushList();
        return html.join('');
    }

    global.LiaraMarkdown = { render: render, escapeHtml: escapeHtml };
})(window);
