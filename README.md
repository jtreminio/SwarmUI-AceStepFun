# SwarmUI-AceStepFun

More advanced AceStep 1.5 text-to-audio support for SwarmUI.

## Audio Prompt Section

`<audio>` is the recommended way to provide the audio-only prompt for AceStepFun.

Examples:

- `a cinematic music video intro <audio>anthemic pop chorus with bright female vocals`
- `moody synthwave scene <audio>slow melancholic lyrics about neon rain`

Behavior:

- Text inside `<audio>...` is used as the AceStepFun audio-only prompt / lyrics section.
- If no `<audio>` section is present, AceStepFun falls back to the normal main prompt.

This extension supports:

- Audio CFG
- Steps
- LM model selection
- Base or SFT model usage

All extension parameters can be set in prompt params, for example:

- `<param[acestepfun audio cfg]:2.5>`
- `<param[acestepfun steps]:12>`
- `<param[acestepfun lm model]:AceStep/qwen_4b_ace15.safetensors>`

It also supports equivalent `text2audio*` params (for example `<param[text2audio style]:pop>` or `<param[text2audio sigma shift]:7>`), with `acestepfun*` values taking priority when both are set.
