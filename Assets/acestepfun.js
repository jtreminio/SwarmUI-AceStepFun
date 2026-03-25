"use strict";

class AceStepFunUI {
    waitForPromptTabComplete() {
        if (typeof promptTabComplete !== "undefined"
            && promptTabComplete
            && typeof promptTabComplete.registerPrefix === "function") {
            this.registerAudioPromptPrefix();
            return;
        }
        setTimeout(() => this.waitForPromptTabComplete(), 100);
    }

    registerAudioPromptPrefix() {
        promptTabComplete.registerPrefix("audio", "Add a section of prompt text that is only used for audio generation in extensions that support it, such as AceStepFun.", () => [
            '\nUse "<audio>..." to provide audio-only prompt text.',
            '\nIn AceStepFun, this becomes the audio-only prompt or lyrics.',
            '\nIf no "<audio>" section is present, AceStepFun falls back to the normal main prompt.'
        ], true);
    }

    constructor() {
        this.waitForPromptTabComplete();
    }
}

new AceStepFunUI();
