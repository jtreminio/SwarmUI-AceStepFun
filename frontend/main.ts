type ValueElement = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
type TimerHandle = ReturnType<typeof setTimeout>;
type ParamType = "decimal" | "dropdown" | "integer" | "text";
type ParamViewType = "prompt" | "slider";

interface AceStepFunTrack {
    Style?: string;
    Duration?: number | string;
    Bpm?: number | string;
    TimeSignature?: string;
    Language?: string;
    KeyScale?: string;
}

interface AceStepFunRootTrack {
    style: ValueElement | null;
    duration: HTMLInputElement | null;
    bpm: HTMLInputElement | null;
    timeSignature: HTMLSelectElement | null;
    language: HTMLSelectElement | null;
    keyScale: HTMLSelectElement | null;
}

interface AceStepFunTrackSnapshot {
    enabled: boolean;
    trackCount: number;
    refs: string[];
}

interface AceStepFunTrackRegistry {
    getSnapshot: () => AceStepFunTrackSnapshot;
}

interface AceStepFunParam {
    id: string;
    name: string;
    description: string;
    type: ParamType;
    default: string;
    min?: number;
    max?: number;
    step?: number;
    view_min?: number;
    view_max?: number;
    view_type?: ParamViewType;
    values?: string[];
    value_names?: string[];
    toggleable: boolean;
    feature_flag: string | null;
}

interface GeneratedParamHtml {
    html: string;
    runnable?: (() => void) | null;
}

interface PromptTabComplete {
    registerPrefix: (
        prefix: string,
        description: string,
        dataProvider: () => string[],
        insertable?: boolean,
    ) => void;
}

declare global {
    interface Window {
        acestepfunTrackRegistry?: AceStepFunTrackRegistry;
    }
}

declare function triggerChangeFor(element: HTMLElement): void;
declare function getHtmlForParam(
    param: AceStepFunParam,
    prefix: string,
): GeneratedParamHtml | null;
declare let postParamBuildSteps: (() => void)[] | undefined;
declare const promptTabComplete: PromptTabComplete | undefined;

const getValueElement = (id: string): ValueElement | null => {
    const elem = document.getElementById(id);
    if (
        elem instanceof HTMLInputElement ||
        elem instanceof HTMLSelectElement ||
        elem instanceof HTMLTextAreaElement
    ) {
        return elem;
    }
    return null;
};

const getInputElement = (id: string): HTMLInputElement | null => {
    const elem = document.getElementById(id);
    return elem instanceof HTMLInputElement ? elem : null;
};

const getSelectElement = (id: string): HTMLSelectElement | null => {
    const elem = document.getElementById(id);
    return elem instanceof HTMLSelectElement ? elem : null;
};

class AceStepFunTrackEditor {
    private readonly eventName = "acestepfun:tracks-changed";
    private readonly toggleId = "input_group_content_acestepfun_toggle";
    private editor: HTMLElement | null = null;
    private changeListenerElem: HTMLElement | null = null;
    private readonly trackSyncTimers = new Map<number, TimerHandle>();
    private tracksInputSyncInterval: TimerHandle | null = null;
    private lastKnownTracksJson = "";
    private lastKnownEnabled = false;

    init(): void {
        this.createEditor();
        this.showTracks();
        this.installTrackChangeListener();
        this.startPublishedTrackSync();
        this.publishTrackAvailability();
    }

    private applyFullWidthLayout(elem: HTMLElement): void {
        elem.style.width = "100%";
        elem.style.maxWidth = "100%";
        elem.style.minWidth = "0";
    }

    private applyEditorLayout(editor: HTMLElement): void {
        this.applyFullWidthLayout(editor);
        editor.style.flex = "1 1 100%";
        editor.style.overflow = "visible";
    }

    private createEditor(): void {
        let editor = document.getElementById("acestepfun_track_editor");
        if (!editor) {
            const group = document.getElementById(
                "input_group_content_acestepfun",
            );
            if (!group) {
                throw new Error("AceStepFun input group was not found.");
            }

            editor = document.createElement("div");
            editor.id = "acestepfun_track_editor";
            editor.className = "acestepfun-track-editor keep_group_visible";
            group.appendChild(editor);
        }
        this.applyEditorLayout(editor);
        this.editor = editor;
    }

    private getRootTrack(): AceStepFunRootTrack {
        return {
            style: getValueElement("input_acestepfunstyle"),
            duration: getInputElement("input_acestepfunduration"),
            bpm: getInputElement("input_acestepfunbpm"),
            timeSignature: getSelectElement("input_acestepfuntimesignature"),
            language: getSelectElement("input_acestepfunlanguage"),
            keyScale: getSelectElement("input_acestepfunkeyscale"),
        };
    }

    private getTracksInput(): HTMLInputElement | null {
        return getInputElement("input_acestepfunmusictracks");
    }

    private readFloat(value: unknown, fallback: number): number {
        const parsed = parseFloat(`${value ?? ""}`);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    private readInt(value: unknown, fallback: number): number {
        const parsed = parseInt(`${value ?? ""}`, 10);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    private createTrack(): AceStepFunTrack {
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

    private getTracks(): AceStepFunTrack[] {
        const tracksInput = this.getTracksInput();
        if (!tracksInput?.value) {
            return [];
        }
        try {
            const tracks = JSON.parse(tracksInput.value) as unknown;
            return Array.isArray(tracks) ? (tracks as AceStepFunTrack[]) : [];
        } catch {
            return [];
        }
    }

    private saveTracks(newTracks: AceStepFunTrack[]): void {
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

    private buildTrackSnapshot(): AceStepFunTrackSnapshot {
        const enabled = this.isAceStepFunGroupEnabled();
        const trackCount = enabled ? this.getTracks().length + 1 : 0;
        const refs: string[] = [];
        for (let i = 0; i < trackCount; i++) {
            refs.push(`audio${i}`);
        }
        return {
            enabled,
            trackCount,
            refs,
        };
    }

    private cloneTrackSnapshot(
        snapshot: AceStepFunTrackSnapshot,
    ): AceStepFunTrackSnapshot {
        return {
            enabled: snapshot.enabled,
            trackCount: snapshot.trackCount,
            refs: [...snapshot.refs],
        };
    }

    private ensureTrackRegistry(): void {
        const getSnapshot = (): AceStepFunTrackSnapshot =>
            this.cloneTrackSnapshot(this.buildTrackSnapshot());
        if (!window.acestepfunTrackRegistry) {
            window.acestepfunTrackRegistry = { getSnapshot };
            return;
        }
        window.acestepfunTrackRegistry.getSnapshot = getSnapshot;
    }

    private publishTrackAvailability(): void {
        this.ensureTrackRegistry();
        const snapshot = this.cloneTrackSnapshot(this.buildTrackSnapshot());
        document.dispatchEvent(
            new CustomEvent(this.eventName, {
                detail: snapshot,
            }),
        );
    }

    private startPublishedTrackSync(): void {
        if (this.tracksInputSyncInterval) {
            return;
        }

        this.lastKnownTracksJson = this.getTracksInput()?.value ?? "";
        this.lastKnownEnabled = this.isAceStepFunGroupEnabled();
        this.tracksInputSyncInterval = setInterval(() => {
            const currentTracksJson = this.getTracksInput()?.value ?? "";
            const aceStepFunEnabled = this.isAceStepFunGroupEnabled();
            if (
                currentTracksJson === this.lastKnownTracksJson &&
                aceStepFunEnabled === this.lastKnownEnabled
            ) {
                return;
            }

            this.lastKnownTracksJson = currentTracksJson;
            this.lastKnownEnabled = aceStepFunEnabled;
            this.publishTrackAvailability();
        }, 150);
    }

    private isAceStepFunGroupEnabled(): boolean {
        const toggler = document.getElementById(this.toggleId);
        return !(toggler instanceof HTMLInputElement) || toggler.checked;
    }

    private installTrackChangeListener(): void {
        if (this.changeListenerElem === this.editor || !this.editor) {
            return;
        }

        const handler = (e: Event): void => {
            try {
                const target = e.target;
                if (!(target instanceof Element)) {
                    return;
                }

                const trackWrap = target.closest<HTMLElement>(
                    "[data-acestepfun-track-id]",
                );
                if (!trackWrap) {
                    return;
                }

                const trackId = parseInt(
                    trackWrap.dataset.acestepfunTrackId ?? "0",
                    10,
                );
                if (trackId < 1) {
                    return;
                }

                if (
                    target.closest(
                        'button[data-acestepfun-action="remove-track"]',
                    )
                ) {
                    return;
                }

                this.scheduleTrackSyncFromUi(trackId);
            } catch {
                // Ignore transient DOM sync failures while controls are rebuilding.
            }
        };

        this.editor.addEventListener("input", handler, true);
        this.editor.addEventListener("change", handler, true);
        this.changeListenerElem = this.editor;
    }

    private scheduleTrackSyncFromUi(trackId: number): void {
        const existing = this.trackSyncTimers.get(trackId);
        if (existing != null) {
            clearTimeout(existing);
        }

        const timer = setTimeout(() => {
            try {
                this.syncSingleTrackFromUi(trackId);
            } catch {
                // Ignore transient DOM sync failures while controls are rebuilding.
            }
        }, 125);

        this.trackSyncTimers.set(trackId, timer);
    }

    private syncSingleTrackFromUi(trackId: number): void {
        const tracks = this.getTracks();
        const idx = trackId - 1;
        if (idx < 0 || idx >= tracks.length) {
            return;
        }

        const prefix = `acestepfun_track_${trackId}_`;
        this.updateTrackFromUi(prefix, tracks[idx]);
        this.saveTracks(tracks);
    }

    private serializeTracksFromUi(): void {
        const tracks = this.getTracks();
        for (let i = 0; i < tracks.length; i++) {
            const trackId = i + 1;
            const prefix = `acestepfun_track_${trackId}_`;
            this.updateTrackFromUi(prefix, tracks[i]);
        }
        this.saveTracks(tracks);
    }

    private showTracks(): void {
        if (!this.editor) {
            return;
        }

        const tracks = this.getTracks();
        const list = document.createElement("div");
        list.className = "acestepfun-track-list";
        this.applyFullWidthLayout(list);

        this.editor.innerHTML = "";
        this.editor.appendChild(list);
        this.addRemoveBtnListener(list);

        for (let idx = 0; idx < tracks.length; idx++) {
            const track = tracks[idx];
            const trackId = idx + 1;
            const wrap = document.createElement("div");
            wrap.className =
                "input-group input-group-open acestepfun-track-wrap";
            wrap.classList.add("border", "rounded", "p-2", "mb-2");
            wrap.id = `acestepfun_track_${trackId}`;
            wrap.dataset.acestepfunTrackId = `${trackId}`;
            this.applyFullWidthLayout(wrap);

            const header = document.createElement("span");
            header.className = "input-group-header input-group-noshrink";
            header.innerHTML =
                '<span class="header-label-wrap">' +
                `<span class="header-label">Music Track ${trackId}</span>` +
                '<span class="header-label-spacer"></span>' +
                `<button class="interrupt-button" title="Remove track" data-acestepfun-action="remove-track" id="acestepfun_remove_track_${trackId}">x</button>` +
                "</span>";
            wrap.appendChild(header);

            const content = document.createElement("div");
            content.className = "input-group-content acestepfun-track-content";
            this.applyFullWidthLayout(content);
            wrap.appendChild(content);

            list.appendChild(wrap);

            const prefix = `acestepfun_track_${trackId}_`;
            const parts = this.buildFieldsForTrack(track, prefix);
            content.insertAdjacentHTML(
                "beforeend",
                parts.map((part) => part.html).join(""),
            );
            for (const part of parts) {
                try {
                    part.runnable?.();
                } catch {
                    // Ignore field enhancement failures; SwarmUI may rebuild inputs.
                }
            }
        }

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

    private addRemoveBtnListener(list: HTMLElement): void {
        list.addEventListener("click", (e) => {
            const target = e.target;
            if (!(target instanceof Element)) {
                return;
            }

            const btn = target.closest<HTMLButtonElement>(
                'button[data-acestepfun-action="remove-track"]',
            );
            if (!btn) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();

            this.serializeTracksFromUi();
            const trackWrap = btn.closest<HTMLElement>(
                "[data-acestepfun-track-id]",
            );
            if (!trackWrap) {
                return;
            }

            const trackId = parseInt(
                trackWrap.dataset.acestepfunTrackId ?? "0",
                10,
            );
            const tracks = this.getTracks();
            tracks.splice(trackId - 1, 1);
            this.saveTracks(tracks);
            this.showTracks();
        });
    }

    private buildFieldsForTrack(
        track: AceStepFunTrack,
        prefix: string,
    ): GeneratedParamHtml[] {
        const rootTrack = this.getRootTrack();
        const durationMin = this.readFloat(rootTrack.duration?.min, 1);
        const durationMax = this.readFloat(rootTrack.duration?.max, 600);
        const durationStep = this.readFloat(rootTrack.duration?.step, 1);
        const bpmMin = this.readFloat(rootTrack.bpm?.min, 40);
        const bpmMax = this.readFloat(rootTrack.bpm?.max, 300);
        const bpmStep = this.readFloat(rootTrack.bpm?.step, 1);
        const timeSignatureValues = Array.from(
            rootTrack.timeSignature?.options ?? [],
        ).map((option) => option.value);
        const timeSignatureLabels = Array.from(
            rootTrack.timeSignature?.options ?? [],
        ).map((option) => option.label);
        const languageValues = Array.from(
            rootTrack.language?.options ?? [],
        ).map((option) => option.value);
        const languageLabels = Array.from(
            rootTrack.language?.options ?? [],
        ).map((option) => option.label);
        const keyScaleValues = Array.from(
            rootTrack.keyScale?.options ?? [],
        ).map((option) => option.value);
        const keyScaleLabels = Array.from(
            rootTrack.keyScale?.options ?? [],
        ).map((option) => option.label);
        const parts: GeneratedParamHtml[] = [];

        parts.push(
            this.buildParam(
                {
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
                },
                prefix,
            ),
        );
        parts.push(
            this.buildParam(
                {
                    id: "style",
                    name: "Style",
                    description: "Style or genre tags.",
                    type: "text",
                    default: `${track.Style ?? rootTrack.style?.value ?? ""}`,
                    toggleable: false,
                    view_type: "prompt",
                    feature_flag: null,
                },
                prefix,
            ),
        );
        parts.push(
            this.buildParam(
                {
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
                },
                prefix,
            ),
        );
        parts.push(
            this.buildParam(
                {
                    id: "timesignature",
                    name: "Time Signature",
                    description: "Time signature.",
                    type: "dropdown",
                    values: timeSignatureValues,
                    value_names: timeSignatureLabels,
                    default:
                        track.TimeSignature ??
                        rootTrack.timeSignature?.value ??
                        "4",
                    toggleable: false,
                    feature_flag: null,
                },
                prefix,
            ),
        );
        parts.push(
            this.buildParam(
                {
                    id: "language",
                    name: "Language",
                    description: "Language for the prompt.",
                    type: "dropdown",
                    values: languageValues,
                    value_names: languageLabels,
                    default:
                        track.Language ?? rootTrack.language?.value ?? "en",
                    toggleable: false,
                    feature_flag: null,
                },
                prefix,
            ),
        );
        parts.push(
            this.buildParam(
                {
                    id: "keyscale",
                    name: "Key Scale",
                    description: "Key and scale for the music.",
                    type: "dropdown",
                    values: keyScaleValues,
                    value_names: keyScaleLabels,
                    default:
                        track.KeyScale ??
                        rootTrack.keyScale?.value ??
                        "E minor",
                    toggleable: false,
                    feature_flag: null,
                },
                prefix,
            ),
        );

        return parts;
    }

    private buildParam(
        param: AceStepFunParam,
        prefix: string,
    ): GeneratedParamHtml {
        const part = getHtmlForParam(param, prefix);
        if (!part) {
            throw new Error(`Unable to build AceStepFun field ${param.id}.`);
        }
        return part;
    }

    private updateTrackFromUi(prefix: string, track: AceStepFunTrack): void {
        const val = (id: string): string | null => {
            const elem = getValueElement(`${prefix}${id}`);
            return elem ? elem.value : null;
        };

        track.Style = `${val("style") ?? track.Style ?? ""}`;
        track.Duration = this.readFloat(
            val("duration"),
            this.readFloat(track.Duration, 120),
        );
        track.Bpm = this.readInt(val("bpm"), this.readInt(track.Bpm, 120));
        track.TimeSignature = `${val("timesignature") ?? track.TimeSignature ?? "4"}`;
        track.Language = `${val("language") ?? track.Language ?? "en"}`;
        track.KeyScale = `${val("keyscale") ?? track.KeyScale ?? "E minor"}`;
    }
}

class AceStepFunUI {
    private trackEditorRegistered = false;

    constructor(private readonly trackEditor: AceStepFunTrackEditor) {
        this.waitForPromptTabComplete();
        this.waitForTrackEditor();
    }

    private waitForPromptTabComplete(): void {
        if (
            typeof promptTabComplete !== "undefined" &&
            promptTabComplete &&
            typeof promptTabComplete.registerPrefix === "function"
        ) {
            this.registerAudioPromptPrefix();
            return;
        }
        setTimeout(() => this.waitForPromptTabComplete(), 100);
    }

    private registerAudioPromptPrefix(): void {
        promptTabComplete.registerPrefix(
            "acestepfun",
            "Add a section of prompt text and scoped parameters for AceStepFun audio generation.",
            () => [
                '\nUse "<acestepfun>..." to provide a shared AceStepFun prompt and scoped parameters for all tracks.',
                '\nUse "<acestepfun[0]>..." for the root track, "<acestepfun[1]>..." for Music Track 1, "<acestepfun[2]>..." for Music Track 2, and so on.',
                '\nParams and LoRAs declared inside "<acestepfun[n]>" only apply to that track.',
            ],
            true,
        );
        promptTabComplete.registerPrefix(
            "audio",
            "Add a section of prompt text that is only used for audio generation in extensions that support it, such as AceStepFun.",
            () => [
                '\nUse "<audio>..." to provide a shared audio-only prompt for all AceStepFun tracks.',
                '\nUse "<acestepfun[0]>...", "<acestepfun[1]>...", etc. for track-specific AceStepFun prompts, params, and LoRAs.',
            ],
            true,
        );
    }

    private waitForTrackEditor(): void {
        if (this.tryRegisterTrackEditor()) {
            return;
        }
        const interval = setInterval(() => {
            if (this.tryRegisterTrackEditor()) {
                clearInterval(interval);
            }
        }, 200);
    }

    private tryRegisterTrackEditor(): boolean {
        if (
            this.trackEditorRegistered ||
            typeof postParamBuildSteps === "undefined" ||
            !Array.isArray(postParamBuildSteps)
        ) {
            return false;
        }
        postParamBuildSteps.push(() => {
            try {
                this.trackEditor.init();
            } catch (error) {
                console.log("AceStepFun: failed to build track editor", error);
            }
        });
        this.trackEditorRegistered = true;
        return true;
    }
}

new AceStepFunUI(new AceStepFunTrackEditor());

export {};
