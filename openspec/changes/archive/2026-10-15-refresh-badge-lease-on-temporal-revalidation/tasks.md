## 1. Refresh expired producer leases

- [x] 1.1 Trigger bound temporal revalidation at the earlier of producer lease expiry and semantic horizon expiry; verify the resolver boundary matches the decision's effective validity boundary.
- [x] 1.2 Preserve producer provenance and use only the validated receipt to reanchor the transport lease; verify the CLI invocation remains bound to the original Health, payload, tree, and producer identities.
