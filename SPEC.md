# Liara AI Assistant — Product Specification

## 1. Overview

Liara AI Assistant is an AI-powered documentation assistant designed to help
Liara Cloud users understand services, discover relevant documentation, solve
technical problems, and complete multi-step tasks.

The system must provide reliable answers grounded in Liara documentation.

---

# 2. Problem

Liara provides a growing number of cloud services and technical documentation.

Users may:

- Fail to find the correct documentation
- Misunderstand technical documentation
- Ask repetitive support questions
- Need help combining multiple documentation pages
- Need step-by-step guidance for technical tasks

The goal is to reduce this friction using an AI-powered assistant.

---

# 3. Main Goals

The system should:

1. Answer questions using Liara documentation.
2. Provide accurate and relevant answers.
3. Show reliable sources.
4. Understand user intent.
5. Maintain conversation context.
6. Ask clarification questions when necessary.
7. Suggest useful next actions.
8. Support multi-step agentic workflows.
9. Provide a polished responsive UI.
10. Be secure and production-ready.
11. Control LLM costs.
12. Be deployable on Liara infrastructure.

---

# 4. Target Users

Primary users:

Liara Cloud users who need help with:

- Deployment
- Databases
- Storage
- Networking
- Domains
- Docker
- Runtime services
- Cloud configuration
- CLI
- Authentication
- Infrastructure-related tasks

---

# 5. Core User Flow

User opens the assistant.

↓

User asks a question.

↓

System analyzes intent.

↓

System determines whether documentation retrieval is required.

↓

Relevant documentation is retrieved.

↓

The system generates a grounded response.

↓

Response contains useful sources.

↓

System suggests a next action when appropriate.

---

# 6. RAG System

## Ingestion

Liara documentation is collected from the official documentation repository.

Pipeline:

Source
→ Parser
→ Cleaner
→ Chunker
→ Metadata
→ Embedding
→ Vector Database

Each chunk should preserve metadata such as:

- Title
- URL
- Section
- Category
- Technology
- Source repository
- Content

---

# 7. Retrieval

User query:

↓

Query embedding

↓

Vector search

↓

Candidate chunks

↓

Optional reranking

↓

Relevant context

↓

LLM

The system should prefer highly relevant and diverse chunks.

---

# 8. Sources

Every documentation-based answer should provide sources when possible.

Sources should include:

- Documentation title
- Relevant URL
- Optional section

The user should be able to open the source.

---

# 9. Agentic Capabilities

The assistant should support:

## Intent Detection

Examples:

- Documentation question
- Troubleshooting
- Deployment
- Configuration
- Pricing
- Comparison
- General question

---

## Clarification

If the request lacks necessary information, the agent should ask a concise
clarifying question.

Example:

User:
"How do I deploy my application?"

Assistant:
"What technology is your application using?"

---

## Documentation Search Tool

Search the indexed Liara documentation.

---

## Documentation Retrieval Tool

Retrieve a specific documentation page or relevant section.

---

## Related Documentation Tool

Find documentation related to the current topic.

---

## Next Action

After answering, suggest the most useful next step when appropriate.

---

# 10. Conversation Context

The assistant should remember relevant information from the current conversation.

Example:

User:
"I have a .NET application."

Assistant:
"Is it containerized with Docker?"

User:
"Yes."

Assistant:
"Then you can deploy it using the Docker workflow..."

The assistant should not repeatedly ask for information already available.

---

# 11. AI Provider

Primary AI provider:

AvalAI

The system must use an abstraction so that the provider can be replaced.

---

# 12. Models

Initial model strategy:

### Main model

GPT-5.5

Used for:

- Final answers
- Complex reasoning
- Agent execution
- Tool calling

### Lightweight model

GPT-5.4-mini

Used for:

- Intent classification
- Simple classification
- Conversation summarization
- Other lightweight tasks

### Embedding

text-embedding-3-small

Initial embedding model.

---

# 13. Streaming

Chat responses should support streaming.

Preferred flow:

Frontend
→ ASP.NET Core
→ AvalAI streaming
→ Frontend

The user should see the answer progressively.

---

# 14. Security

Required:

- API key protection
- Rate limiting
- Input validation
- Request limits
- Token limits
- Safe error handling
- Secure environment configuration

---

# 15. Monitoring

The system should record useful operational metrics.

Examples:

- Request count
- Response time
- LLM latency
- Retrieval latency
- Token usage
- Model used
- Errors
- Cache hits
- Agent execution
- Tool calls

Never log secrets.

---

# 16. Cost Optimization

The system should minimize unnecessary AI calls.

Strategies:

- Model routing
- Caching
- Context reduction
- Token limits
- Retrieval before generation
- Avoid unnecessary agent loops
- Lightweight models for simple tasks

---

# 17. UI

The UI should provide:

- Chat interface
- Markdown rendering
- Code highlighting
- Source cards
- Loading states
- Error states
- Streaming
- Conversation history
- Suggested questions
- Agent actions
- Responsive layout

The design should be clean and professional.

---

# 18. Deployment

The complete application must be deployable on Liara.

Expected production components:

- Web/API application
- PostgreSQL
- Redis
- Environment configuration

The final submission should provide:

- Deployed application URL
- GitHub repository URL

---

# 19. Evaluation Targets

The implementation should explicitly target:

### Answer Quality — 80

Accuracy, relevance, completeness, sources, hallucination reduction.

### UI/UX — 55

Usability, responsive design, code rendering, sources, conversation experience.

### Agentic / Personalization — 50

Intent, clarification, context, personalization, next actions, workflows.

### Security / Stability / Monitoring — 50

Rate limiting, secrets, failure handling, token control, logging and monitoring.

### Deployment — 40

Production deployment on Liara.

### Cost Optimization — 25

Model selection, caching, token control, infrastructure efficiency.

---

# 20. Non-Goals

The initial version should NOT attempt to:

- Replace Liara support completely
- Execute destructive infrastructure operations
- Automatically modify user infrastructure without confirmation
- Build a fully autonomous unrestricted agent
- Support every possible external cloud provider

The assistant should prioritize reliability and safety over excessive autonomy.