# Repository agent guidance

## Architecture review checkpoints

- Review large architectural plans before implementation and review the evidence before retrying a repeated failure.
- Architect model selection is unrestricted. The main agent's model may also perform the Architect role; Luna + Luna is explicitly permitted. A move to a more expensive model is never required.
- Start a separate Architect agent only when it is likely to add material value. Avoid unnecessary multi-agent calls.
- If no Architect tool is available, a documented architecture self-review by the main agent is permitted. Record the evidence, decisions, risks, and unresolved questions. Label it as a self-review; never describe it as an independent review.
- Before declaring a long task complete, review the changes and validation evidence. The self-review option above also applies when an Architect tool is unavailable.
- These review options do not waive security, architecture, test, or quality requirements, and do not authorize bypassing repository protections.
