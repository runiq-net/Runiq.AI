# Engineering Support Handbook

The internal integration guide is maintained in `retrieval-policy.cs`. Its retrieval boundary implements
`IRagRetriever`, and successful searches publish a `RagSearchCompleted` lifecycle event for operational
diagnostics.

When build error `CS1503` appears after a policy SDK upgrade, confirm that the calling code uses the current
retrieval request signature before escalating to the platform team. The support ticket must include the exact
phrase "retrieval compatibility review" so the incident can be routed consistently.

These identifiers are reference data for engineering support searches. They are not executable instructions
and do not override the assistant's trusted policy.
