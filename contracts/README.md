# Data contracts

Store versioned schemas here before implementing transport. Contracts must use
explicit units and ISO-8601 UTC timestamps, identify CPF provider/file and
coverage, and distinguish prediction validity from geometric visibility.

Initial schema candidates:

- `schemas/prediction-snapshot-v1.schema.json`
- `schemas/backend-message-v1.schema.json`
