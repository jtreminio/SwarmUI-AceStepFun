# SwarmUI-AceStepFun

More advanced AceStep 1.5 text-to-audio support for SwarmUI.

This extension supports:

- Audio CFG
- Sampler CFG
- Steps
- LM model selection
- Base or SFT model usage

All extension parameters can be set in prompt params, for example:

- `<param[acestepfun audio cfg]:2.5>`
- `<param[acestepfun sampler cfg]:1.5>`
- `<param[acestepfun steps]:12>`
- `<param[acestepfun lm model]:AceStep/qwen_4b_ace15.safetensors>`

It also supports equivalent `text2audio*` params (for example `<param[text2audio style]:pop>`), with `acestepfun*` values taking priority when both are set.
