# Corporate Document Assistant

This ASP.NET Core sample hosts the Corporate Document Assistant scenario with the Runiq embedded dashboard and the Runiq RAG library.

The scenario models an internal IT support assistant that can work with corporate text documents such as VPN troubleshooting instructions, password policy notes, software request procedures, and external access guidance. The sample can ingest plain-text documents, chunk them, create deterministic demo embeddings, upsert vector records into an in-memory vector store under a configurable `indexName`, run retrieval, and return sourced demo answers.

The sample is intended to stay provider-independent. Demo providers can be replaced later with real embedding providers, vector stores, and answer generation providers without moving demo-specific logic into `src/Runiq.AI.Rag`. Dashboard hosting is composed through `src/Runiq.AI.Core`; RAG ingestion remains composed through `src/Runiq.AI.Rag`.

The dashboard agent uses the production `UseRag` path with `RagExecutionMode.Required`,
`RagNoContextBehavior.ReturnNotFound`, and a minimum relevance score. If the index has not been seeded or no
candidate passes acceptance, Agent Chat returns the controlled not-found outcome before model invocation. The
framework supplies the grounding and untrusted-document instructions; the agent definition does not repeat them.
The sample's separate `/query` endpoint remains a deterministic retrieval-inspection endpoint and does not invoke
the Agent Chat model runtime.

## Run

From the repository root:

```bash
dotnet run --project samples/Runiq.AI.Rag.CorporateDocumentAssistant/Runiq.AI.Rag.CorporateDocumentAssistant.csproj
```

Open the displayed local URL, then use `/dashboard` to view the embedded Runiq Dashboard and `/documents` to inspect the checked-in seed documents. The dashboard exposes these files through the `Corporate Documents` context space, matching the host composition style used by `Runiq.AI.ContextTravelGuide`.

## Ingestion

Inspect the ingestion configuration:

```bash
curl http://localhost:5000/ingestion
```

Ingest all checked-in seed documents:

```bash
curl -X POST http://localhost:5000/ingestion/seed
```

Ingest one plain-text document:

```bash
curl -X POST http://localhost:5000/ingestion/documents \
  -H "Content-Type: application/json" \
  -d "{\"id\":\"custom-policy\",\"title\":\"Custom Policy\",\"content\":\"Plain text policy content.\"}"
```

Ask a question after ingesting documents:

```bash
curl -X POST http://localhost:5000/query \
  -H "Content-Type: application/json" \
  -d "{\"question\":\"VPN baglantisi calismiyorsa ne yapmaliyim?\",\"topK\":4}"
```

## Seed Documents

The `SampleDocuments` folder includes Turkish markdown procedure documents for IT support, access management, password security, VPN, remote work, software requests, data classification, phishing, device assignment, and incident reporting.

These documents are intentionally markdown/text content so the sample can demonstrate a minimal RAG flow without production document parsing or external services.
