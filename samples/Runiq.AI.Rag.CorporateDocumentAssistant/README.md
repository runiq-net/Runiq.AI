# Corporate Document Assistant

This sample is a complete OpenAI-backed RAG application built with Runiq. At application startup it ingests the bundled corporate policies into an in-memory vector store, exposes the registered index in RAG Management, and makes the grounded assistant available in Agent Chat.

It demonstrates the production framework paths for:

- directory document-source registration;
- the typed `OpenAiEmbeddingModels.TextEmbedding3Small` selection;
- a singleton in-memory vector store;
- managed `OnStartup` ingestion;
- required-context Agent Chat behavior;
- RAG lifecycle events in the Dashboard timeline.

No seed request or separate query endpoint is required. The Dashboard and Agent Chat are the primary sample experience.

## Prerequisites

- .NET 10 SDK
- An OpenAI API key with access to `gpt-4.1-mini` and `text-embedding-3-small`

## Configure the API key

The sample reads `OpenAI:ApiKey`. Do not put the key in source control.

PowerShell:

```powershell
$env:OpenAI__ApiKey = "your-api-key"
```

macOS or Linux:

```bash
export OpenAI__ApiKey="your-api-key"
```

User-secrets are also supported:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key" --project samples/Runiq.AI.Rag.CorporateDocumentAssistant
```

## Run the sample

```powershell
dotnet run --project samples/Runiq.AI.Rag.CorporateDocumentAssistant
```

Then:

1. Open the `/dashboard` URL printed by ASP.NET Core.
2. Open **RAG Management**.
3. Verify that `corporate-documents` is `Ready`, its strategy is `On startup`, and its last startup operation is `Completed`.
4. Open **Agents**, select **Corporate Document Assistant**, and open Agent Chat.
5. Ask a question and expand the RAG lifecycle entry to inspect candidate, acceptance, rejection, selected document/chunk, and duration data.

Try these questions:

- How many remote work days are employees allowed?
- What expenses require manager approval?
- What should an employee do after a security incident?
- What is the company policy for bringing pets to the office?

The first three questions are covered by the bundled documents. The last question is intentionally not covered; the required-context policy should return a no-context result instead of asking the model to invent an answer.

## Runtime behavior

The `corporate-documents` index uses a directory source rooted at `AppContext.BaseDirectory/SampleDocuments`, so it does not depend on the process working directory. The project copies the documents to the output directory.

The small corpus is ingested synchronously with `OnStartup`. ASP.NET Core does not begin serving Dashboard or Agent Chat requests until ingestion completes. A missing document directory, cancellation, or embedding failure fails startup instead of leaving an apparently ready application with an unusable index.

The in-memory store is shared by ingestion and retrieval for the application lifetime. Restarting the process creates a new store and runs startup ingestion again. Starting ingestion again from RAG Management exercises the normal incremental same-hash skip behavior.

Retrieved documents are untrusted context. The framework keeps them below system instructions, and the sample agent is explicitly instructed not to follow instructions found in retrieved content.

## Troubleshooting

### OpenAI API key is missing

Startup fails with a configuration message describing `OpenAI:ApiKey`. Set the environment variable or user-secret before starting the sample.

### SampleDocuments is missing

Build the sample again and confirm that `SampleDocuments/*.md` exists beside the sample assembly. The source path is intentionally resolved from the application base directory.

### Startup ingestion fails

Check the server log for the startup exception. Common causes are invalid OpenAI credentials, unavailable model access, network failure, or missing bundled documents. Provider response bodies and credentials are not exposed through Dashboard management payloads.

### The index is not Ready

Because ingestion is `OnStartup`, the application should not finish startup with a failed initial operation. Restart the application after correcting the configuration error and verify the completed operation in RAG Management.
