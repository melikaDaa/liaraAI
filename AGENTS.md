# AGENTS.md

## Project

Liara AI Assistant is an AI-powered assistant for Liara Cloud documentation.

The goal is to help users understand Liara services, find relevant documentation,
solve technical problems, and complete multi-step tasks.

The project is built for a technical competition, so quality, reliability,
security, UX, cost optimization, and deployment are first-class concerns.

---

# Technology Stack

- .NET 10
- ASP.NET Core Web API
- Clean Architecture
- PostgreSQL
- pgvector
- Redis
- Docker
- Vanilla JavaScript
- HTML
- CSS
- AvalAI API
- Git / GitHub

---

# Architecture

The solution follows Clean Architecture.

Dependency direction:

API
→ Application
→ Domain

Infrastructure
→ Application
→ Domain

Domain must not depend on any other project.

Application must not depend on Infrastructure.

Infrastructure contains implementations of external services and technical concerns.

API is responsible for HTTP concerns only.

---

# Project Responsibilities

## Domain

Contains:

- Entities
- Value Objects
- Domain Exceptions
- Domain Enums
- Domain-level interfaces when appropriate

Do not put infrastructure or framework-specific logic here.

---

## Application

Contains:

- Use Cases
- Application Services
- DTOs
- Interfaces
- Validators
- Agent orchestration contracts
- RAG contracts

Business/application logic belongs here.

Do not directly call external APIs from Application.

---

## Infrastructure

Contains implementations for:

- PostgreSQL
- Entity Framework Core
- pgvector
- Redis
- AvalAI
- Embeddings
- LLM
- Logging
- External services

External providers must be accessed through abstractions.

---

## API

Contains:

- Controllers / Endpoints
- Dependency Injection configuration
- Authentication / Authorization
- Middleware
- Rate Limiting
- API configuration

Controllers must remain thin.

Do not put business logic inside controllers.

---

# AI Architecture

The AI system must be provider-agnostic.

Never couple Application directly to AvalAI.

Use abstractions such as:

- IChatModel
- IEmbeddingService
- IVectorStore
- IDocumentSearch
- IAgentOrchestrator

AvalAI implementations belong in Infrastructure.

---

# LLM Provider

Primary provider:

AvalAI

Base URL:

https://api.avalai.ir/v1

API keys must never be committed to Git.

Use environment variables or secure configuration.

Example:

AVALAI_API_KEY

Never expose API keys to the frontend.

---

# RAG

The documentation assistant must use Retrieval-Augmented Generation.

Pipeline:

Documentation
→ Loading
→ Parsing
→ Chunking
→ Embedding
→ Vector Storage
→ Retrieval
→ Context
→ LLM
→ Answer + Sources

Answers should be grounded in retrieved documentation.

Avoid hallucination.

If reliable documentation cannot be found, the assistant should clearly state
that it could not find enough information.

---

# Agent

The system should support agentic behavior.

The agent may:

- Detect intent
- Search documentation
- Retrieve specific documentation pages
- Find related documentation
- Ask clarification questions
- Maintain conversation context
- Suggest next actions
- Execute multi-step workflows when appropriate

Tools must be isolated behind interfaces.

Do not create unnecessary autonomous loops.

Agent execution must have limits.

---

# Conversation

Conversation context must be maintained.

The system should avoid sending unnecessary historical messages to the LLM.

Use summarization or context reduction when conversations become large.

---

# Security

Never:

- Commit secrets
- Expose API keys
- Trust raw user input
- Allow unlimited requests
- Log sensitive information

Implement:

- Rate limiting
- Input validation
- Secure configuration
- Error handling
- Request size limits
- Token usage limits

---

# Cost Optimization

The system should minimize unnecessary LLM calls.

Prefer:

- Caching
- Small models for simple tasks
- Context reduction
- Retrieval before generation
- Limited conversation history
- Token limits

Do not call the strongest model when a simpler model is sufficient.

---

# Error Handling

Do not expose internal exceptions to users.

Return meaningful API errors.

Use structured logging.

External API failures must be handled gracefully.

The application should remain stable when:

- LLM is unavailable
- Vector database is unavailable
- Redis is unavailable
- Documentation search fails
- External APIs timeout

---

# Testing

Important application logic must have tests.

At minimum test:

- RAG retrieval
- Chunking
- Intent detection
- Agent tool selection
- Conversation context
- Error handling
- API behavior

Prefer unit tests for business logic.

Use integration tests for database and external infrastructure behavior.

---

# Coding Standards

Use:

- Clear naming
- Small classes
- Small methods
- Dependency Injection
- Async/await
- CancellationToken
- Nullable reference types
- Immutable models where appropriate

Avoid:

- God classes
- Massive services
- Static global state
- Magic strings
- Duplicate logic
- Unnecessary abstractions

---

# API Design

Use RESTful conventions.

Use DTOs for API contracts.

Do not expose Domain entities directly through API responses.

Validate incoming requests.

Support CancellationToken.

---

# Database

PostgreSQL is the primary database.

pgvector is used for semantic document retrieval.

Entity Framework Core is the ORM.

Database migrations must be version controlled.

---

# Redis

Redis may be used for:

- Response caching
- Conversation-related temporary state
- Rate limiting
- Performance optimization

Do not make Redis a mandatory dependency for core business correctness unless explicitly required.

---

# Frontend

Use:

- HTML
- CSS
- Vanilla JavaScript

Do not introduce React, Vue, Angular, or another frontend framework unless explicitly decided.

Frontend must be:

- Responsive
- Accessible
- Simple
- Fast

Chat UI must support:

- Markdown
- Code blocks
- Links
- Sources
- Loading states
- Error states
- Conversation history
- Streaming responses

---

# Git

Use meaningful commits.

Examples:

feat: add document ingestion pipeline

feat: implement semantic search

fix: handle AvalAI timeout

test: add RAG retrieval tests

refactor: simplify agent orchestration

Do not commit:

- API keys
- .env files
- credentials
- generated secrets

---

# Agent Rules

Before implementing a feature:

1. Understand the existing architecture.
2. Inspect relevant files.
3. Do not modify unrelated code.
4. Follow existing conventions.
5. Prefer the smallest correct implementation.
6. Add or update tests when appropriate.
7. Build the solution after changes.
8. Report what was changed and any remaining issues.

Never rewrite the entire project without explicit instruction.

Do not introduce new libraries without a clear reason.

When uncertain about architecture, stop and explain the trade-off instead of making
large architectural changes silently.