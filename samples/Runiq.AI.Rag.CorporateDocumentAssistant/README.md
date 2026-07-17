# Corporate Document Assistant

## What this sample demonstrates

This sample is an OpenAI-backed RAG application built entirely on the production Runiq runtime. It registers the typed `corporate-documents` index, discovers Markdown files from a directory, selects `OpenAiEmbeddingModels.TextEmbedding3Small`, uses the DI-managed in-memory vector store, and ingests on startup. The same index then powers RAG Management, Agent Chat, the RAG lifecycle timeline, and grounding evidence.

There is no seed endpoint, debug retrieval endpoint, or sample-only RAG path.

## Prerequisites

- .NET 10 SDK
- An OpenAI API key with access to `gpt-4.1-mini` and `text-embedding-3-small`

## Configure OpenAI

The sample reads `OpenAI:ApiKey`. Do not store the key in source control.

PowerShell:

```powershell
$env:OpenAI__ApiKey = "your-api-key"
```

Bash:

```bash
export OpenAI__ApiKey="your-api-key"
```

User secrets:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key" --project samples/Runiq.AI.Rag.CorporateDocumentAssistant
```

## Run the sample

```powershell
dotnet run --project samples/Runiq.AI.Rag.CorporateDocumentAssistant
```

Open the `/dashboard` URL printed by ASP.NET Core. Startup fails with a short configuration error when the API key is missing; the key is not included in that error.

## Verify the RAG index

Open **RAG Management** and select `corporate-documents`. Verify:

- source type is `Directory`;
- ingestion strategy is `On startup`;
- vector store is `InMemory`;
- embedding reference is `openai/text-embedding-3-small`;
- readiness is `Ready` and the last operation is `Completed`;
- discovered-document, chunk, and embedding counters are positive.

Starting ingestion again from this screen uses the same managed runtime. Unchanged documents are reported as `Skipped` because their hashes have not changed.

## Ask questions in Agent Chat

Open **Agents**, select **Corporate Document Assistant**, and open Agent Chat. A covered answer shows the RAG started/completed timeline, selected and rejected results, and a **Grounded with N sources** card. The card identifies `corporate-documents`, the selected document and chunk, context order, available score/relevance values, and that the source was included in model context.

The evidence represents the actual accepted model context. It is not a formal `[1]` citation system.

## Covered example questions

- How many remote work days are employees allowed?
- Which expenses require manager approval?
- What should an employee do after discovering a security incident?

## Uncovered example question

- What is the company's parental leave policy?

For the uncovered question, the required-context policy returns a not-found answer without invoking the model. Agent Chat shows **No grounding context**, the structured no-context reason, and an empty selected-source list.

## How startup ingestion works

The `SampleDocuments` path is resolved from `AppContext.BaseDirectory`, so behavior does not depend on the current working directory. The project copies the bundled Markdown files to the build output. During host startup the managed ingestion runtime discovers documents, chunks them, creates OpenAI embeddings, writes them to the configured store, and marks the index ready only after the operation completes. A missing directory, embedding failure, or partial failure prevents false readiness.

Retrieved document text remains untrusted external context. It is delimited below system authority, and document-embedded instructions are not promoted into system instructions.

## In-memory store lifetime

Ingestion and retrieval resolve the same DI-managed store instance for the application lifetime. Restarting the application creates an empty store and runs `OnStartup` ingestion again.

## Troubleshooting

- **OpenAI API key is missing:** configure `OpenAI:ApiKey` with an environment variable or user secret.
- **`SampleDocuments` is missing:** rebuild the sample and confirm the folder exists beside the sample assembly.
- **Startup ingestion fails:** check the server log for credential, model-access, network, or document errors. Credentials and provider response bodies are not exposed in Dashboard payloads.
- **The index is not ready:** correct the startup error and restart. A failed initial ingestion intentionally prevents the application from appearing ready.
