"use strict";

class AceStepFunDomUtils {
    static getInputElement(id) {
        return document.getElementById(id);
    }

    static getSelectElement(id) {
        return document.getElementById(id);
    }
}

class AceStepFunTrackEditor {
    constructor() {
        this.eventName = "acestepfun:tracks-changed";
        this.toggleId = "input_group_content_acestepfun_toggle";
        this.editor = null;
        this.changeListenerElem = null;
        this.trackSyncTimers = new Map();
        this.tracksInputSyncInterval = null;
        this.lastKnownTracksJson = "";
        this.lastKnownEnabled = false;
    }

    init() {
        this.createEditor();
        this.showTracks();
        this.installTrackChangeListener();
        this.startPublishedTrackSync();
        this.publishTrackAvailability();
    }

    applyFullWidthLayout(elem) {
        elem.style.width = "100%";
        elem.style.maxWidth = "100%";
        elem.style.minWidth = "0";
    }

    applyEditorLayout(editor) {
        this.applyFullWidthLayout(editor);
        editor.style.flex = "1 1 100%";
        editor.style.overflow = "visible";
    }

    createEditor() {
        let editor = document.getElementById("acestepfun_track_editor");
        if (!editor) {
            editor = document.createElement("div");
            editor.id = "acestepfun_track_editor";
            editor.className = "acestepfun-track-editor keep_group_visible";
            document.getElementById("input_group_content_acestepfun").appendChild(editor);
        }
        this.applyEditorLayout(editor);
        this.editor = editor;
    }

    getRootTrack() {
        return {
            style: AceStepFunDomUtils.getInputElement("input_acestepfunstyle"),
            duration: AceStepFunDomUtils.getInputElement("input_acestepfunduration"),
            bpm: AceStepFunDomUtils.getInputElement("input_acestepfunbpm"),
            timeSignature: AceStepFunDomUtils.getSelectElement("input_acestepfuntimesignature"),
            language: AceStepFunDomUtils.getSelectElement("input_acestepfunlanguage"),
            keyScale: AceStepFunDomUtils.getSelectElement("input_acestepfunkeyscale"),
        };
    }

    getTracksInput() {
        return AceStepFunDomUtils.getInputElement("input_acestepfunmusictracks");
    }

    readFloat(value, fallback) {
        const parsed = parseFloat(`${value ?? ""}`);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    readInt(value, fallback) {
        const parsed = parseInt(`${value ?? ""}`, 10);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    createTrack() {
        const rootTrack = this.getRootTrack();
        return {
            Style: rootTrack.style?.value ?? "",
            Duration: this.readFloat(rootTrack.duration?.value, 120),
            Bpm: this.readInt(rootTrack.bpm?.value, 120),
            TimeSignature: rootTrack.timeSignature?.value ?? "4",
            Language: rootTrack.language?.value ?? "en",
            KeyScale: rootTrack.keyScale?.value ?? "E minor",
        };
    }

    getTracks() {
        const tracksInput = this.getTracksInput();
        if (!tracksInput || !tracksInput.value) {
            return [];
        }
        try {
            const tracks = JSON.parse(tracksInput.value);
            return Array.isArray(tracks) ? tracks : [];
        }
        catch {
            return [];
        }
    }

    saveTracks(newTracks) {
        const tracksInput = this.getTracksInput();
        if (!tracksInput) {
            return;
        }
        tracksInput.value = JSON.stringify(newTracks);
        this.lastKnownTracksJson = tracksInput.value;
        this.lastKnownEnabled = this.isAceStepFunGroupEnabled();
        if (this.isAceStepFunGroupEnabled()) {
            triggerChangeFor(tracksInput);
        }
        this.publishTrackAvailability();
    }

    buildTrackSnapshot() {
        const enabled = this.isAceStepFunGroupEnabled();
        const trackCount = enabled ? this.getTracks().length + 1 : 0;
        const refs = [];
        for (let i = 0; i < trackCount; i++) {
            refs.push(`audio${i}`);
        }
        return {
            enabled,
            trackCount,
            refs,
        };
    }

    cloneTrackSnapshot(snapshot) {
        return {
            enabled: snapshot.enabled,
            trackCount: snapshot.trackCount,
            refs: [...snapshot.refs],
        };
    }

    ensureTrackRegistry() {
        const getSnapshot = () => this.cloneTrackSnapshot(this.buildTrackSnapshot());
        if (!window.acestepfunTrackRegistry) {
            window.acestepfunTrackRegistry = { getSnapshot };
            return;
        }
        window.acestepfunTrackRegistry.getSnapshot = getSnapshot;
    }

    publishTrackAvailability() {
        this.ensureTrackRegistry();
        const snapshot = this.cloneTrackSnapshot(this.buildTrackSnapshot());
        document.dispatchEvent(new CustomEvent(this.eventName, {
            detail: snapshot,
        }));
    }

    startPublishedTrackSync() {
        if (this.tracksInputSyncInterval) {
            return;
        }

        this.lastKnownTracksJson = this.getTracksInput()?.value ?? "";
        this.lastKnownEnabled = this.isAceStepFunGroupEnabled();
        this.tracksInputSyncInterval = setInterval(() => {
            const currentTracksJson = this.getTracksInput()?.value ?? "";
            const aceStepFunEnabled = this.isAceStepFunGroupEnabled();
            if (currentTracksJson == this.lastKnownTracksJson
                && aceStepFunEnabled == this.lastKnownEnabled
            ) {
                return;
            }

            this.lastKnownTracksJson = currentTracksJson;
            this.lastKnownEnabled = aceStepFunEnabled;
            this.publishTrackAvailability();
        }, 150);
    }

    isAceStepFunGroupEnabled() {
        const toggler = document.getElementById(this.toggleId);
        return !toggler || !!toggler.checked;
    }

    installTrackChangeListener() {
        if (this.changeListenerElem === this.editor) {
            return;
        }

        const handler = (e) => {
            try {
                const target = e.target;
                if (!target) {
                    return;
                }

                const trackWrap = target.closest("[data-acestepfun-track-id]");
                if (!trackWrap) {
                    return;
                }

                const trackId = parseInt(trackWrap.dataset.acestepfunTrackId ?? "0", 10);
                if (trackId < 1) {
                    return;
                }

                if (target.closest('button[data-acestepfun-action="remove-track"]')) {
                    return;
                }

                this.scheduleTrackSyncFromUi(trackId);
            }
            catch { }
        };

        this.editor.addEventListener("input", handler, true);
        this.editor.addEventListener("change", handler, true);
        this.changeListenerElem = this.editor;
    }

    scheduleTrackSyncFromUi(trackId) {
        const existing = this.trackSyncTimers.get(trackId);
        if (existing) {
            clearTimeout(existing);
        }

        const timer = setTimeout(() => {
            try {
                this.syncSingleTrackFromUi(trackId);
            }
            catch { }
        }, 125);

        this.trackSyncTimers.set(trackId, timer);
    }

    syncSingleTrackFromUi(trackId) {
        const tracks = this.getTracks();
        const idx = trackId - 1;
        if (idx < 0 || idx >= tracks.length) {
            return;
        }

        const prefix = `acestepfun_track_${trackId}_`;
        this.updateTrackFromUi(prefix, tracks[idx]);
        this.saveTracks(tracks);
    }

    serializeTracksFromUi() {
        const tracks = this.getTracks();
        for (let i = 0; i < tracks.length; i++) {
            const trackId = i + 1;
            const prefix = `acestepfun_track_${trackId}_`;
            this.updateTrackFromUi(prefix, tracks[i]);
        }
        this.saveTracks(tracks);
    }

    showTracks() {
        const tracks = this.getTracks();
        const list = document.createElement("div");
        list.className = "acestepfun-track-list";
        this.applyFullWidthLayout(list);

        this.editor.innerHTML = "";
        this.editor.appendChild(list);
        this.addRemoveBtnListener(list);

        tracks.forEach((track, idx) => {
            const trackId = idx + 1;
            const wrap = document.createElement("div");
            wrap.className = "input-group input-group-open acestepfun-track-wrap";
            wrap.classList.add("border", "rounded", "p-2", "mb-2");
            wrap.id = `acestepfun_track_${trackId}`;
            wrap.dataset.acestepfunTrackId = `${trackId}`;
            this.applyFullWidthLayout(wrap);

            const header = document.createElement("span");
            header.className = "input-group-header input-group-noshrink";
            header.innerHTML =
                `<span class="header-label-wrap">`
                + `<span class="header-label">Music Track ${trackId}</span>`
                + `<span class="header-label-spacer"></span>`
                + `<button class="interrupt-button" title="Remove track" data-acestepfun-action="remove-track" id="acestepfun_remove_track_${trackId}">x</button>`
                + `</span>`;
            wrap.appendChild(header);

            const content = document.createElement("div");
            content.className = "input-group-content acestepfun-track-content";
            this.applyFullWidthLayout(content);
            wrap.appendChild(content);

            list.appendChild(wrap);

            const prefix = `acestepfun_track_${trackId}_`;
            const parts = this.buildFieldsForTrack(track, prefix);
            content.insertAdjacentHTML("beforeend", parts.map((part) => part.html).join(""));
            for (const part of parts) {
                try {
                    part.runnable?.();
                }
                catch { }
            }
        });

        const addBtn = document.createElement("button");
        addBtn.className = "basic-button";
        addBtn.innerText = "+ Add Music Track";
        addBtn.addEventListener("click", (e) => {
            e.preventDefault();
            this.serializeTracksFromUi();
            const current = this.getTracks();
            this.saveTracks([...current, this.createTrack()]);
            this.showTracks();
        });
        this.editor.appendChild(addBtn);
    }

    addRemoveBtnListener(list) {
        list.addEventListener("click", (e) => {
            const btn = e.target.closest('button[data-acestepfun-action="remove-track"]');
            if (!btn) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();

            this.serializeTracksFromUi();
            const trackId = parseInt(btn.closest("[data-acestepfun-track-id]").dataset.acestepfunTrackId, 10);
            const tracks = this.getTracks();
            tracks.splice(trackId - 1, 1);
            this.saveTracks(tracks);
            this.showTracks();
        });
    }

    buildFieldsForTrack(track, prefix) {
        const rootTrack = this.getRootTrack();
        const durationMin = this.readFloat(rootTrack.duration?.min, 1);
        const durationMax = this.readFloat(rootTrack.duration?.max, 600);
        const durationStep = this.readFloat(rootTrack.duration?.step, 1);
        const bpmMin = this.readFloat(rootTrack.bpm?.min, 40);
        const bpmMax = this.readFloat(rootTrack.bpm?.max, 300);
        const bpmStep = this.readFloat(rootTrack.bpm?.step, 1);
        const timeSignatureValues = Array.from(rootTrack.timeSignature?.options ?? []).map((option) => option.value);
        const timeSignatureLabels = Array.from(rootTrack.timeSignature?.options ?? []).map((option) => option.label);
        const languageValues = Array.from(rootTrack.language?.options ?? []).map((option) => option.value);
        const languageLabels = Array.from(rootTrack.language?.options ?? []).map((option) => option.label);
        const keyScaleValues = Array.from(rootTrack.keyScale?.options ?? []).map((option) => option.value);
        const keyScaleLabels = Array.from(rootTrack.keyScale?.options ?? []).map((option) => option.label);
        const parts = [];

        parts.push(getHtmlForParam({
            id: "duration",
            name: "Duration",
            description: "Audio duration in seconds.",
            type: "decimal",
            default: `${track.Duration ?? this.readFloat(rootTrack.duration?.value, 120)}`,
            min: durationMin,
            max: durationMax,
            step: durationStep,
            view_min: durationMin,
            view_max: durationMax,
            view_type: "slider",
            toggleable: false,
            feature_flag: null,
        }, prefix));
        parts.push(getHtmlForParam({
            id: "style",
            name: "Style",
            description: "Style or genre tags.",
            type: "text",
            default: `${track.Style ?? rootTrack.style?.value ?? ""}`,
            toggleable: false,
            view_type: "prompt",
            feature_flag: null,
        }, prefix));
        parts.push(getHtmlForParam({
            id: "bpm",
            name: "BPM",
            description: "Beats per minute.",
            type: "integer",
            default: `${track.Bpm ?? this.readInt(rootTrack.bpm?.value, 120)}`,
            min: bpmMin,
            max: bpmMax,
            step: bpmStep,
            view_min: bpmMin,
            view_max: bpmMax,
            view_type: "slider",
            toggleable: false,
            feature_flag: null,
        }, prefix));
        parts.push(getHtmlForParam({
            id: "timesignature",
            name: "Time Signature",
            description: "Time signature.",
            type: "dropdown",
            values: timeSignatureValues,
            value_names: timeSignatureLabels,
            default: track.TimeSignature ?? rootTrack.timeSignature?.value ?? "4",
            toggleable: false,
            feature_flag: null,
        }, prefix));
        parts.push(getHtmlForParam({
            id: "language",
            name: "Language",
            description: "Language for the prompt.",
            type: "dropdown",
            values: languageValues,
            value_names: languageLabels,
            default: track.Language ?? rootTrack.language?.value ?? "en",
            toggleable: false,
            feature_flag: null,
        }, prefix));
        parts.push(getHtmlForParam({
            id: "keyscale",
            name: "Key Scale",
            description: "Key and scale for the music.",
            type: "dropdown",
            values: keyScaleValues,
            value_names: keyScaleLabels,
            default: track.KeyScale ?? rootTrack.keyScale?.value ?? "E minor",
            toggleable: false,
            feature_flag: null,
        }, prefix));

        return parts;
    }

    updateTrackFromUi(prefix, track) {
        const val = (id) => {
            const elem = AceStepFunDomUtils.getInputElement(`${prefix}${id}`);
            return elem ? elem.value : null;
        };

        track.Style = `${val("style") ?? track.Style ?? ""}`;
        track.Duration = this.readFloat(val("duration"), this.readFloat(track.Duration, 120));
        track.Bpm = this.readInt(val("bpm"), this.readInt(track.Bpm, 120));
        track.TimeSignature = `${val("timesignature") ?? track.TimeSignature ?? "4"}`;
        track.Language = `${val("language") ?? track.Language ?? "en"}`;
        track.KeyScale = `${val("keyscale") ?? track.KeyScale ?? "E minor"}`;
    }
}

class AceStepFunUI {
    constructor(trackEditor) {
        this.trackEditor = trackEditor;
        this.trackEditorRegistered = false;
        this.waitForPromptTabComplete();
        this.waitForTrackEditor();
    }

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
        promptTabComplete.registerPrefix("acestepfun", "Add a section of prompt text and scoped parameters for AceStepFun audio generation.", () => [
            '\nUse "<acestepfun>..." to provide a shared AceStepFun prompt and scoped parameters for all tracks.',
            '\nUse "<acestepfun[0]>..." for the root track, "<acestepfun[1]>..." for Music Track 1, "<acestepfun[2]>..." for Music Track 2, and so on.',
            '\nParams and LoRAs declared inside "<acestepfun[n]>" only apply to that track.'
        ], true);
        promptTabComplete.registerPrefix("audio", "Add a section of prompt text that is only used for audio generation in extensions that support it, such as AceStepFun.", () => [
            '\nUse "<audio>..." to provide a shared audio-only prompt for all AceStepFun tracks.',
            '\nUse "<acestepfun[0]>...", "<acestepfun[1]>...", etc. for track-specific AceStepFun prompts, params, and LoRAs.'
        ], true);
    }

    waitForTrackEditor() {
        if (this.tryRegisterTrackEditor()) {
            return;
        }
        const interval = setInterval(() => {
            if (this.tryRegisterTrackEditor()) {
                clearInterval(interval);
            }
        }, 200);
    }

    tryRegisterTrackEditor() {
        if (this.trackEditorRegistered
            || typeof postParamBuildSteps === "undefined"
            || !Array.isArray(postParamBuildSteps)) {
            return false;
        }
        postParamBuildSteps.push(() => {
            try {
                this.trackEditor.init();
            }
            catch (e) {
                console.log("AceStepFun: failed to build track editor", e);
            }
        });
        this.trackEditorRegistered = true;
        return true;
    }
}

new AceStepFunUI(new AceStepFunTrackEditor());
