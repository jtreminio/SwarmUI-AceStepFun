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

class AceStepFunTrackPublisher {
    constructor() {
        this.eventName = "acestepfun:tracks-changed";
        this.refPrefix = "acestepfun";
        this.toggleId = "input_group_content_acestepfun_toggle";
        this.lastSnapshotJson = "";
        this.ensureRegistry();
        this.publish();
        this.startPolling();
    }

    isAceStepFunGroupEnabled() {
        const toggler = document.getElementById(this.toggleId);
        return !toggler || !!toggler.checked;
    }

    buildSnapshot() {
        const enabled = this.isAceStepFunGroupEnabled();
        const trackCount = enabled ? 1 : 0;
        const refs = [];
        for (let i = 0; i < trackCount; i++) {
            refs.push(`${this.refPrefix}${i}`);
        }
        return { enabled, trackCount, refs };
    }

    cloneSnapshot(snapshot) {
        return {
            enabled: snapshot.enabled,
            trackCount: snapshot.trackCount,
            refs: [...snapshot.refs],
        };
    }

    ensureRegistry() {
        const getSnapshot = () => this.cloneSnapshot(this.buildSnapshot());
        if (!window.acestepfunTrackRegistry) {
            window.acestepfunTrackRegistry = { getSnapshot };
            return;
        }
        window.acestepfunTrackRegistry.getSnapshot = getSnapshot;
    }

    publish() {
        this.ensureRegistry();
        const snapshot = this.cloneSnapshot(this.buildSnapshot());
        this.lastSnapshotJson = JSON.stringify(snapshot);
        document.dispatchEvent(new CustomEvent(this.eventName, { detail: snapshot }));
    }

    startPolling() {
        setInterval(() => {
            const currentJson = JSON.stringify(this.buildSnapshot());
            if (currentJson === this.lastSnapshotJson) {
                return;
            }
            this.publish();
        }, 150);
    }
}

new AceStepFunUI();
new AceStepFunTrackPublisher();
