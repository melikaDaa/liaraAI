/*
 * Liara AI — Chat UI controller.
 *
 * Works entirely with MOCK responses for this milestone. The send pipeline is
 * intentionally isolated in `getAssistantResponse()` so it can later be swapped
 * for a real backend/RAG call without touching the rendering logic.
 */
(function () {
    'use strict';

    var els = {
        chat: document.getElementById('chat'),
        welcome: document.getElementById('welcome'),
        messages: document.getElementById('messages'),
        form: document.getElementById('composerForm'),
        input: document.getElementById('input'),
        sendBtn: document.getElementById('sendBtn'),
        newChatBtn: document.getElementById('newChatBtn'),
        suggestions: document.getElementById('suggestions'),
        sidebar: document.getElementById('sidebar'),
        sidebarClose: document.getElementById('sidebarClose'),
        sidebarOverlay: document.getElementById('sidebarOverlay'),
        menuBtn: document.getElementById('menuBtn')
    };

    var state = {
        awaitingResponse: false,
        hasMessages: false,
        conversationId: null,
        conversations: []
    };

    /* ---------- Mock response knowledge base ---------- */
    var MOCK_DEFAULT = [
        'این یک پاسخ نمونه است. در نسخه‌ی نهایی، پاسخ بر اساس مستندات واقعی لیارا و با استفاده از جست‌وجوی معنایی تولید می‌شود.',
        '',
        'در حال حاضر می‌توانید رابط کاربری، قالب‌بندی **Markdown**، بلاک‌های کد و فهرست‌ها را بررسی کنید.',
        '',
        'برای مطالعه‌ی بیشتر به [مستندات لیارا](https://docs.liara.ir) مراجعه کنید.'
    ].join('\n');

    var MOCK_RESPONSES = [
        {
            match: /docker|داکر|کانتینر|deploy|استقرار|دیپلوی/i,
            text: [
                'برای استقرار یک اپلیکیشن **Docker** روی لیارا مراحل زیر را دنبال کنید:',
                '',
                '### مراحل',
                '',
                '1. یک `Dockerfile` در ریشه‌ی پروژه بسازید.',
                '2. فایل `liara.json` را با پلتفرم `docker` تنظیم کنید.',
                '3. با استفاده از CLI لیارا، پروژه را دیپلوی کنید.',
                '',
                '```bash',
                'docker build -t my-app .',
                'liara deploy --platform=docker',
                '```',
                '',
                'برای جزئیات بیشتر، [مستندات استقرار Docker](https://docs.liara.ir) را ببینید.'
            ].join('\n'),
            sources: [
                { title: 'Deploying a Docker application', url: 'https://docs.liara.ir/deploy/docker' }
            ]
        },
        {
            match: /متغیر|environment|env|محیط/i,
            text: [
                'برای تنظیم **متغیرهای محیطی** در لیارا دو روش دارید:',
                '',
                '- از طریق **کنسول لیارا** در بخش تنظیمات برنامه.',
                '- از طریق فایل `liara.json` با کلید `envs`.',
                '',
                'نمونه‌ای از تنظیم متغیر در خط فرمان:',
                '',
                '```bash',
                'liara env:set NODE_ENV=production',
                '```',
                '',
                'مقادیر حساس مانند کلیدهای API را همیشه به‌صورت متغیر محیطی نگه‌داری کنید.'
            ].join('\n'),
            sources: [
                { title: 'Environment Variables', url: 'https://docs.liara.ir/deploy/envs' }
            ]
        },
        {
            match: /postgres|پستگرس|دیتابیس|database|sql|اتصال/i,
            text: [
                'برای اتصال به **PostgreSQL** در لیارا:',
                '',
                '1. یک دیتابیس PostgreSQL از بخش دیتابیس‌ها بسازید.',
                '2. اطلاعات اتصال (`host`, `port`, `user`, `password`) را از کنسول کپی کنید.',
                '3. رشته‌ی اتصال را به‌صورت متغیر محیطی به برنامه بدهید.',
                '',
                '```bash',
                'DATABASE_URL="postgresql://user:pass@host:5432/dbname"',
                '```',
                '',
                'توصیه می‌شود اتصال از طریق شبکه‌ی داخلی لیارا انجام شود.'
            ].join('\n'),
            sources: [
                { title: 'PostgreSQL Database', url: 'https://docs.liara.ir/database/postgresql' },
                { title: 'Connection Strings', url: 'https://docs.liara.ir/database/connection' }
            ]
        },
        {
            match: /سرویس|خدمات|services|چه کار|امکانات/i,
            text: [
                'لیارا مجموعه‌ای از سرویس‌های ابری را ارائه می‌دهد:',
                '',
                '- **PaaS** برای استقرار برنامه‌ها (Node.js، Laravel، Django، .NET و…)',
                '- **دیتابیس‌ها**: PostgreSQL، MySQL، MongoDB، Redis و…',
                '- **ذخیره‌سازی ابری** سازگار با S3',
                '- **سرویس ایمیل** و **مدیریت دامنه و DNS**',
                '- **سرویس هوش مصنوعی**',
                '',
                'هر سرویس مستندات اختصاصی خود را دارد.'
            ].join('\n'),
            sources: [
                { title: 'Liara Services Overview', url: 'https://docs.liara.ir/introduction' }
            ]
        }
    ];

    function pickMockResponse(text) {
        for (var i = 0; i < MOCK_RESPONSES.length; i++) {
            if (MOCK_RESPONSES[i].match.test(text)) {
                return MOCK_RESPONSES[i];
            }
        }
        return { text: MOCK_DEFAULT, sources: [] };
    }

    /*
     * Real backend RAG call. Falls back to mock if the API is unreachable.
     * Maintains conversation context via conversationId.
     */
    function getAssistantResponse(userText) {
        var body = { message: userText };
        if (state.conversationId) {
            body.conversationId = state.conversationId;
        }

        return fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        })
        .then(function (res) {
            if (!res.ok) throw new Error('API error ' + res.status);
            return res.json();
        })
        .then(function (data) {
            if (data.conversationId) {
                state.conversationId = data.conversationId;
            }
            return {
                text: data.answer || 'پاسخی دریافت نشد.',
                sources: (data.sources || []).map(function (s) {
                    return { title: s.title, url: s.url, heading: s.heading, headingPath: s.headingPath };
                })
            };
        })
        .catch(function () {
            return pickMockResponse(userText);
        });
    }

    /* ---------- Rendering ---------- */
    function formatTime(date) {
        try {
            return new Intl.DateTimeFormat('fa-IR', {
                hour: '2-digit',
                minute: '2-digit'
            }).format(date);
        } catch (e) {
            return '';
        }
    }

    function showConversation() {
        if (!state.hasMessages) {
            state.hasMessages = true;
            els.welcome.hidden = true;
        }
    }

    function scrollToBottom() {
        els.chat.scrollTop = els.chat.scrollHeight;
    }

    function appendUserMessage(text) {
        var row = document.createElement('div');
        row.className = 'msg msg--user';

        var avatar = document.createElement('div');
        avatar.className = 'msg__avatar';
        avatar.textContent = 'شما';

        var body = document.createElement('div');
        body.className = 'msg__body';

        var bubble = document.createElement('div');
        bubble.className = 'msg__bubble';
        bubble.textContent = text;

        var time = document.createElement('span');
        time.className = 'msg__time';
        time.textContent = formatTime(new Date());

        body.appendChild(bubble);
        body.appendChild(time);
        row.appendChild(avatar);
        row.appendChild(body);
        els.messages.appendChild(row);
        scrollToBottom();
    }

    function appendTypingIndicator() {
        var row = document.createElement('div');
        row.className = 'msg msg--assistant';
        row.setAttribute('data-typing', 'true');

        var avatar = document.createElement('div');
        avatar.className = 'msg__avatar';
        avatar.textContent = 'AI';

        var body = document.createElement('div');
        body.className = 'msg__body';

        var bubble = document.createElement('div');
        bubble.className = 'msg__bubble';
        bubble.innerHTML = '<div class="typing"><span></span><span></span><span></span></div>';

        body.appendChild(bubble);
        row.appendChild(avatar);
        row.appendChild(body);
        els.messages.appendChild(row);
        scrollToBottom();
        return row;
    }

    function appendAssistantMessage(response) {
        var text = typeof response === 'string' ? response : response.text;
        var sources = (typeof response === 'object' && response.sources) ? response.sources : [];

        var row = document.createElement('div');
        row.className = 'msg msg--assistant';

        var avatar = document.createElement('div');
        avatar.className = 'msg__avatar';
        avatar.textContent = 'AI';

        var body = document.createElement('div');
        body.className = 'msg__body';

        var bubble = document.createElement('div');
        bubble.className = 'msg__bubble';
        bubble.innerHTML = window.LiaraMarkdown.render(text);

        var time = document.createElement('span');
        time.className = 'msg__time';
        time.textContent = formatTime(new Date());

        body.appendChild(bubble);
        body.appendChild(time);

        /* Render sources if available */
        if (sources.length > 0) {
            var sourcesEl = document.createElement('div');
            sourcesEl.className = 'sources';
            sourcesEl.innerHTML = '<p class="sources__title">منابع</p>';

            var list = document.createElement('div');
            list.className = 'sources__list';

            for (var i = 0; i < sources.length; i++) {
                var src = sources[i];
                var link = document.createElement('a');
                link.className = 'source';
                link.href = src.url;
                link.target = '_blank';
                link.rel = 'noopener noreferrer';
                link.innerHTML =
                    '<span class="source__label">' +
                        '<span class="source__dot" aria-hidden="true"></span>' +
                        '<span>' + LiaraMarkdown.escapeHtml(src.title) + '</span>' +
                    '</span>' +
                    '<svg class="source__arrow" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M7 17L17 7M17 7H7M17 7v10"/></svg>';
                list.appendChild(link);
            }

            sourcesEl.appendChild(list);
            body.appendChild(sourcesEl);
        }

        row.appendChild(avatar);
        row.appendChild(body);
        els.messages.appendChild(row);
        scrollToBottom();
    }

    /* ---------- Send flow ---------- */
    function sendMessage(text) {
        var trimmed = text.trim();
        if (!trimmed || state.awaitingResponse) return;

        showConversation();
        appendUserMessage(trimmed);

        els.input.value = '';
        autoResize();
        updateSendButton();

        state.awaitingResponse = true;
        updateSendButton();
        var typingRow = appendTypingIndicator();

        getAssistantResponse(trimmed).then(function (response) {
            if (typingRow && typingRow.parentNode) {
                typingRow.parentNode.removeChild(typingRow);
            }
            appendAssistantMessage(response);
        }).catch(function () {
            if (typingRow && typingRow.parentNode) {
                typingRow.parentNode.removeChild(typingRow);
            }
            appendAssistantMessage('متأسفانه در دریافت پاسخ خطایی رخ داد. لطفاً دوباره تلاش کنید.');
        }).finally(function () {
            state.awaitingResponse = false;
            updateSendButton();
            els.input.focus();
        });
    }

    /* ---------- Composer helpers ---------- */
    function autoResize() {
        els.input.style.height = 'auto';
        els.input.style.height = Math.min(els.input.scrollHeight, 180) + 'px';
    }

    function updateSendButton() {
        var hasText = els.input.value.trim().length > 0;
        els.sendBtn.disabled = !hasText || state.awaitingResponse;
    }

    function resetChat() {
        els.messages.innerHTML = '';
        state.hasMessages = false;
        state.awaitingResponse = false;
        state.conversationId = null;
        els.welcome.hidden = false;
        els.input.value = '';
        autoResize();
        updateSendButton();
        els.input.focus();
        closeSidebar();
    }

    /* ---------- Conversation Management ---------- */
    function loadConversations() {
        fetch('/api/conversations')
            .then(function (res) {
                if (!res.ok) throw new Error('Failed to load conversations');
                return res.json();
            })
            .then(function (conversations) {
                state.conversations = conversations;
                renderConversationList();
            })
            .catch(function () {
                state.conversations = [];
                renderConversationList();
            });
    }

    function renderConversationList() {
        var listEl = document.getElementById('recentChats');
        if (!listEl) return;

        if (state.conversations.length === 0) {
            listEl.innerHTML = '<p class="sidebar__empty">هنوز گفتگویی ندارید.</p>';
            return;
        }

        listEl.innerHTML = '';
        state.conversations.forEach(function (conv) {
            var item = document.createElement('div');
            item.className = 'sidebar__item';
            item.setAttribute('data-conversation-id', conv.id);

            var title = document.createElement('span');
            title.className = 'sidebar__item-title';
            title.textContent = conv.title;

            var deleteBtn = document.createElement('button');
            deleteBtn.className = 'sidebar__item-delete';
            deleteBtn.setAttribute('aria-label', 'حذف گفتگو');
            deleteBtn.innerHTML = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>';

            deleteBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                deleteConversation(conv.id);
            });

            item.appendChild(title);
            item.appendChild(deleteBtn);

            item.addEventListener('click', function () {
                loadConversation(conv.id);
            });

            listEl.appendChild(item);
        });
    }

    function deleteConversation(id) {
        fetch('/api/conversations/' + id, { method: 'DELETE' })
            .then(function (res) {
                if (!res.ok) throw new Error('Failed to delete');
                if (state.conversationId === id) {
                    resetChat();
                }
                loadConversations();
            })
            .catch(function () {});
    }

    function loadConversation(id) {
        fetch('/api/conversations/' + id)
            .then(function (res) {
                if (!res.ok) throw new Error('Failed to load');
                return res.json();
            })
            .then(function (conversation) {
                state.conversationId = id;
                els.messages.innerHTML = '';
                state.hasMessages = false;
                showConversation();
                els.welcome.hidden = true;

                if (conversation.messages && conversation.messages.length > 0) {
                    conversation.messages.forEach(function (msg) {
                        if (msg.role === 'user') {
                            appendUserMessage(msg.content);
                        } else {
                            appendAssistantMessage({ text: msg.content, sources: [] });
                        }
                    });
                }
                closeSidebar();
            })
            .catch(function () {});
    }

    /* ---------- Sidebar ---------- */
    function openSidebar() {
        els.sidebar.classList.add('is-open');
        els.sidebarOverlay.classList.add('is-visible');
        els.sidebarOverlay.setAttribute('aria-hidden', 'false');
    }

    function closeSidebar() {
        els.sidebar.classList.remove('is-open');
        els.sidebarOverlay.classList.remove('is-visible');
        els.sidebarOverlay.setAttribute('aria-hidden', 'true');
    }

    /* ---------- Events ---------- */
    els.form.addEventListener('submit', function (e) {
        e.preventDefault();
        sendMessage(els.input.value);
    });

    els.input.addEventListener('input', function () {
        autoResize();
        updateSendButton();
    });

    els.input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage(els.input.value);
        }
    });

    els.suggestions.addEventListener('click', function (e) {
        var btn = e.target.closest('.suggestion');
        if (btn) {
            sendMessage(btn.querySelector('.suggestion__text').textContent.trim());
        }
    });

    els.newChatBtn.addEventListener('click', resetChat);

    els.messages.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-copy]');
        if (!btn) return;
        var block = btn.closest('.code-block');
        var code = block ? block.querySelector('code') : null;
        if (!code) return;

        var text = code.textContent;
        var done = function () {
            var original = btn.textContent;
            btn.textContent = 'کپی شد';
            setTimeout(function () { btn.textContent = original; }, 1500);
        };

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(done).catch(function () {});
        }
    });

    /* Sidebar events */
    if (els.menuBtn) {
        els.menuBtn.addEventListener('click', openSidebar);
    }
    if (els.sidebarClose) {
        els.sidebarClose.addEventListener('click', closeSidebar);
    }
    if (els.sidebarOverlay) {
        els.sidebarOverlay.addEventListener('click', closeSidebar);
    }

    /* Close sidebar on Escape */
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && els.sidebar.classList.contains('is-open')) {
            closeSidebar();
        }
    });

    /* Load conversations on page load */
    loadConversations();

    els.input.focus();
})();
