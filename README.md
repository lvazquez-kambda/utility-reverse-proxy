# Utility tool for debugging lp-platform

Reverse Proxy to intercept GraphQl specific queries and redirect them to localhost or the desired dev-enviroment

In `appsettings.json`

```json
  "ProxyConfiguration": {
    "DefaultURL": "https://{0}/graphql", // <- Here you define the URL to redirect everything you don't want to redirect, the {0} will be replace with the stage
    "Stage": "dev", // desired stage
    "RedirectQueries": [ // Queries you want to intercept and redirect
      {
        "Query": "FETCH_CONTRACT_REVIEW",
        "RedirectURL": "http://localhost:4013/graphql" // this query will be redirected to localhost:4013
      }
    ]
  }

```
