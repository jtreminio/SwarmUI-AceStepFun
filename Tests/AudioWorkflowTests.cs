using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Newtonsoft.Json.Linq;
using SwarmUI.Core;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;
using Xunit;

namespace AceStepFun.Tests;

public class AudioWorkflowTests
{
    [Fact]
    public void GetUserParam_UsesText2AudioFallback_ForAllSupportedPairs()
    {
        EnsureParamsRegistered();

        T2IParamInput input = new(null);
        object jsonParser = CreateJsonParser(input);

        input.Set(AceStepFunExtension.Text2AudioPrompt, "fallback prompt");
        input.Set(T2IParamTypes.Text2AudioStyle, "fallback style");
        input.Set(T2IParamTypes.Text2AudioDuration, 77.0);
        input.Set(T2IParamTypes.Text2AudioBPM, 141L);
        input.Set(T2IParamTypes.Text2AudioTimeSignature, "6");
        input.Set(T2IParamTypes.Text2AudioLanguage, "ja");
        input.Set(T2IParamTypes.Text2AudioKeyScale, "C major");
        input.Set(AceStepFunExtension.Text2AudioLmModel, "AceStep/qwen_4b_ace15.safetensors");
        input.Set(AceStepFunExtension.Text2AudioAudioCfg, 4.5);
        input.Set(AceStepFunExtension.Text2AudioLmCfg, 3.5);
        input.Set(AceStepFunExtension.Text2AudioSteps, 33L);

        Assert.Equal("fallback prompt", InvokeGetUserParam(jsonParser, AceStepFunExtension.Prompt, AceStepFunExtension.Text2AudioPrompt));
        Assert.Equal("fallback style", InvokeGetUserParam(jsonParser, AceStepFunExtension.Style, T2IParamTypes.Text2AudioStyle));
        Assert.Equal(77.0, InvokeGetUserParam(jsonParser, AceStepFunExtension.Duration, T2IParamTypes.Text2AudioDuration));
        Assert.Equal(141L, InvokeGetUserParam(jsonParser, AceStepFunExtension.Bpm, T2IParamTypes.Text2AudioBPM));
        Assert.Equal("6", InvokeGetUserParam(jsonParser, AceStepFunExtension.TimeSignature, T2IParamTypes.Text2AudioTimeSignature));
        Assert.Equal("ja", InvokeGetUserParam(jsonParser, AceStepFunExtension.Language, T2IParamTypes.Text2AudioLanguage));
        Assert.Equal("C major", InvokeGetUserParam(jsonParser, AceStepFunExtension.KeyScale, T2IParamTypes.Text2AudioKeyScale));
        Assert.Equal("AceStep/qwen_4b_ace15.safetensors", InvokeGetUserParam(jsonParser, AceStepFunExtension.LmModel, AceStepFunExtension.Text2AudioLmModel));
        Assert.Equal(3.5, InvokeGetUserParam(jsonParser, AceStepFunExtension.LmCfg, AceStepFunExtension.Text2AudioLmCfg));
        Assert.Equal(4.5, InvokeGetUserParam(jsonParser, AceStepFunExtension.AudioCfg, AceStepFunExtension.Text2AudioAudioCfg));
        Assert.Equal(33L, InvokeGetUserParam(jsonParser, AceStepFunExtension.Steps, AceStepFunExtension.Text2AudioSteps));
    }

    [Fact]
    public void GetUserParam_PrefersAceStepFunPrimary_AndUsesFallbackWhenPrimaryEqualsRegisteredDefault()
    {
        EnsureParamsRegistered();

        T2IParamInput input = new(null);
        object jsonParser = CreateJsonParser(input);

        input.Set(AceStepFunExtension.Prompt, "primary prompt");
        input.Set(AceStepFunExtension.Text2AudioPrompt, "fallback prompt");
        Assert.Equal("primary prompt", InvokeGetUserParam(jsonParser, AceStepFunExtension.Prompt, AceStepFunExtension.Text2AudioPrompt));
        input.Set(AceStepFunExtension.Prompt, "");
        Assert.Equal("fallback prompt", InvokeGetUserParam(jsonParser, AceStepFunExtension.Prompt, AceStepFunExtension.Text2AudioPrompt));

        input.Set(AceStepFunExtension.Style, "primary style");
        input.Set(T2IParamTypes.Text2AudioStyle, "fallback style");
        Assert.Equal("primary style", InvokeGetUserParam(jsonParser, AceStepFunExtension.Style, T2IParamTypes.Text2AudioStyle));
        input.Set(AceStepFunExtension.Style, "");
        Assert.Equal("fallback style", InvokeGetUserParam(jsonParser, AceStepFunExtension.Style, T2IParamTypes.Text2AudioStyle));

        input.Set(AceStepFunExtension.Duration, 42.0);
        input.Set(T2IParamTypes.Text2AudioDuration, 77.0);
        Assert.Equal(42.0, InvokeGetUserParam(jsonParser, AceStepFunExtension.Duration, T2IParamTypes.Text2AudioDuration));
        input.Set(AceStepFunExtension.Duration, 120.0);
        Assert.Equal(77.0, InvokeGetUserParam(jsonParser, AceStepFunExtension.Duration, T2IParamTypes.Text2AudioDuration));

        input.Set(AceStepFunExtension.Bpm, 150L);
        input.Set(T2IParamTypes.Text2AudioBPM, 141L);
        Assert.Equal(150L, InvokeGetUserParam(jsonParser, AceStepFunExtension.Bpm, T2IParamTypes.Text2AudioBPM));
        input.Set(AceStepFunExtension.Bpm, 120L);
        Assert.Equal(141L, InvokeGetUserParam(jsonParser, AceStepFunExtension.Bpm, T2IParamTypes.Text2AudioBPM));

        input.Set(AceStepFunExtension.TimeSignature, "3");
        input.Set(T2IParamTypes.Text2AudioTimeSignature, "6");
        Assert.Equal("3", InvokeGetUserParam(jsonParser, AceStepFunExtension.TimeSignature, T2IParamTypes.Text2AudioTimeSignature));
        input.Set(AceStepFunExtension.TimeSignature, "4");
        Assert.Equal("6", InvokeGetUserParam(jsonParser, AceStepFunExtension.TimeSignature, T2IParamTypes.Text2AudioTimeSignature));

        input.Set(AceStepFunExtension.Language, "de");
        input.Set(T2IParamTypes.Text2AudioLanguage, "ja");
        Assert.Equal("de", InvokeGetUserParam(jsonParser, AceStepFunExtension.Language, T2IParamTypes.Text2AudioLanguage));
        input.Set(AceStepFunExtension.Language, "en");
        Assert.Equal("ja", InvokeGetUserParam(jsonParser, AceStepFunExtension.Language, T2IParamTypes.Text2AudioLanguage));

        input.Set(AceStepFunExtension.KeyScale, "A minor");
        input.Set(T2IParamTypes.Text2AudioKeyScale, "C major");
        Assert.Equal("A minor", InvokeGetUserParam(jsonParser, AceStepFunExtension.KeyScale, T2IParamTypes.Text2AudioKeyScale));
        input.Set(AceStepFunExtension.KeyScale, "E minor");
        Assert.Equal("C major", InvokeGetUserParam(jsonParser, AceStepFunExtension.KeyScale, T2IParamTypes.Text2AudioKeyScale));

        input.Set(AceStepFunExtension.LmModel, "AceStep/qwen_0.6b_ace15.safetensors");
        input.Set(AceStepFunExtension.Text2AudioLmModel, "AceStep/qwen_4b_ace15.safetensors");
        Assert.Equal("AceStep/qwen_0.6b_ace15.safetensors", InvokeGetUserParam(jsonParser, AceStepFunExtension.LmModel, AceStepFunExtension.Text2AudioLmModel));
        input.Set(AceStepFunExtension.LmModel, "AceStep/qwen_1.7b_ace15.safetensors");
        Assert.Equal("AceStep/qwen_4b_ace15.safetensors", InvokeGetUserParam(jsonParser, AceStepFunExtension.LmModel, AceStepFunExtension.Text2AudioLmModel));

        input.Set(AceStepFunExtension.LmCfg, 1.0);
        input.Set(AceStepFunExtension.Text2AudioLmCfg, 4.5);
        Assert.Equal(1.0, InvokeGetUserParam(jsonParser, AceStepFunExtension.LmCfg, AceStepFunExtension.Text2AudioLmCfg));
        input.Set(AceStepFunExtension.LmCfg, 2.0);
        Assert.Equal(4.5, InvokeGetUserParam(jsonParser, AceStepFunExtension.LmCfg, AceStepFunExtension.Text2AudioLmCfg));

        input.Set(AceStepFunExtension.AudioCfg, 2.5);
        input.Set(AceStepFunExtension.Text2AudioAudioCfg, 3.5);
        Assert.Equal(2.5, InvokeGetUserParam(jsonParser, AceStepFunExtension.AudioCfg, AceStepFunExtension.Text2AudioAudioCfg));
        input.Set(AceStepFunExtension.AudioCfg, 1.0);
        Assert.Equal(3.5, InvokeGetUserParam(jsonParser, AceStepFunExtension.AudioCfg, AceStepFunExtension.Text2AudioAudioCfg));

        input.Set(AceStepFunExtension.Steps, 12L);
        input.Set(AceStepFunExtension.Text2AudioSteps, 33L);
        Assert.Equal(12L, InvokeGetUserParam(jsonParser, AceStepFunExtension.Steps, AceStepFunExtension.Text2AudioSteps));
        input.Set(AceStepFunExtension.Steps, 8L);
        Assert.Equal(33L, InvokeGetUserParam(jsonParser, AceStepFunExtension.Steps, AceStepFunExtension.Text2AudioSteps));
    }

    [Fact]
    public void Text2AudioLmCfg_AcceptsLegacyText2AudioSamplerCfgId()
    {
        EnsureParamsRegistered();

        T2IParamInput input = new(null);
        Assert.True(T2IParamTypes.TryGetType("textaudiosamplercfg", out T2IParamType type, input));
        Assert.Equal(AceStepFunExtension.Text2AudioLmCfg.Type.ID, type.ID);
    }

    [Fact]
    public void AceStepFunLmCfg_AcceptsLegacyAceStepFunSamplerCfgId()
    {
        EnsureParamsRegistered();

        T2IParamInput input = new(null);
        Assert.True(T2IParamTypes.TryGetType("acestepfunsamplercfg", out T2IParamType type, input));
        Assert.Equal(AceStepFunExtension.LmCfg.Type.ID, type.ID);
    }

    [Fact]
    public void ResolvePrompt_UsesAudioSectionFromMainPrompt_WhenExplicitPromptsUnset()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input);
        input.Set(T2IParamTypes.Prompt, "scene setup <audio>chorus lyrics");
        input.PreparsePromptLikes();

        Assert.Equal("chorus lyrics", InvokeResolvePrompt(audioWorkflow));
    }

    [Fact]
    public void ResolvePrompt_FallsBackToMainPrompt_WhenAudioSectionMissing()
    {
        EnsureParamsRegistered();

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input);
        input.Set(T2IParamTypes.Prompt, "plain main prompt");
        input.PreparsePromptLikes();

        Assert.Equal("plain main prompt", InvokeResolvePrompt(audioWorkflow));
    }

    [Fact]
    public void ResolvePrompt_PrefersExplicitPromptParams_OverMainPromptAudioSection()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input);
        input.Set(T2IParamTypes.Prompt, "global setup <audio>section prompt");
        input.Set(AceStepFunExtension.Text2AudioPrompt, "fallback prompt");
        input.PreparsePromptLikes();

        Assert.Equal("fallback prompt", InvokeResolvePrompt(audioWorkflow));

        input.Set(AceStepFunExtension.Prompt, "primary prompt");
        Assert.Equal("primary prompt", InvokeResolvePrompt(audioWorkflow));
    }

    [Fact]
    public void ResolvePrompt_FallsBackToMainPrompt_WhenExplicitPromptParamsAreRawButEmpty()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input);
        input.Set(T2IParamTypes.Prompt, "global setup <audio>section prompt");
        input.Set(AceStepFunExtension.Prompt, "");
        input.Set(AceStepFunExtension.Text2AudioPrompt, "");
        input.PreparsePromptLikes();

        Assert.Equal("section prompt", InvokeResolvePrompt(audioWorkflow));
    }

    [Fact]
    public void ResolvePrompt_UsesAceStepFunSection_ForRootAndAdditionalTracks()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input);
        input.Set(T2IParamTypes.Prompt, "global setup <acestepfun>shared section <acestepfun[1]>track one section");
        input.PreparsePromptLikes();

        Assert.Equal("shared section", InvokeResolvePrompt(audioWorkflow));
        Assert.Equal("track one section", InvokeResolvePrompt(audioWorkflow, 1));
    }

    [Fact]
    public void GetTracks_ParsesAdditionalMusicTracks_AndInheritsMissingValues()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input);
        input.Set(T2IParamTypes.Prompt, "global setup <audio>shared audio section <acestepfun[1]>track one audio section");
        input.Set(AceStepFunExtension.Style, "cinematic");
        input.Set(AceStepFunExtension.Duration, 90.0);
        input.Set(AceStepFunExtension.Bpm, 110L);
        input.Set(AceStepFunExtension.TimeSignature, "3");
        input.Set(AceStepFunExtension.Language, "en");
        input.Set(AceStepFunExtension.KeyScale, "A minor");
        input.Set(AceStepFunExtension.MusicTracks, new JArray(
            new JObject
            {
                ["Style"] = "ambient",
                ["Bpm"] = 140L,
                ["Language"] = "ja"
            },
            new JObject
            {
                ["Duration"] = 45.0
            }
        ).ToString());
        input.PreparsePromptLikes();

        List<Dictionary<string, object>> tracks = InvokeGetTracks(audioWorkflow);

        Assert.Equal(3, tracks.Count);
        Assert.Equal(0, tracks[0]["Index"]);
        Assert.Equal(1, tracks[1]["Index"]);
        Assert.Equal(2, tracks[2]["Index"]);

        Assert.Equal("shared audio section", tracks[0]["Prompt"]);
        Assert.Equal("cinematic", tracks[0]["Style"]);
        Assert.Equal(90.0, tracks[0]["Duration"]);
        Assert.Equal(110L, tracks[0]["Bpm"]);
        Assert.Equal("3", tracks[0]["TimeSignature"]);
        Assert.Equal("en", tracks[0]["Language"]);
        Assert.Equal("A minor", tracks[0]["KeyScale"]);

        Assert.Equal("track one audio section", tracks[1]["Prompt"]);
        Assert.Equal("ambient", tracks[1]["Style"]);
        Assert.Equal(90.0, tracks[1]["Duration"]);
        Assert.Equal(140L, tracks[1]["Bpm"]);
        Assert.Equal("3", tracks[1]["TimeSignature"]);
        Assert.Equal("ja", tracks[1]["Language"]);
        Assert.Equal("A minor", tracks[1]["KeyScale"]);

        Assert.Equal("shared audio section", tracks[2]["Prompt"]);
        Assert.Equal("ambient", tracks[2]["Style"]);
        Assert.Equal(45.0, tracks[2]["Duration"]);
        Assert.Equal(140L, tracks[2]["Bpm"]);
        Assert.Equal("3", tracks[2]["TimeSignature"]);
        Assert.Equal("ja", tracks[2]["Language"]);
        Assert.Equal("A minor", tracks[2]["KeyScale"]);
    }

    [Fact]
    public void PreparsePromptLikes_RewritesAudioTagWithAceStepSectionId()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        T2IParamInput input = new(null);
        input.Set(T2IParamTypes.Prompt, "global <audio>chorus lyrics");
        input.PreparsePromptLikes();

        string parsedPrompt = input.Get(T2IParamTypes.Prompt, "");
        Assert.Contains($"<audio//cid={AceStepFunExtension.SectionID_Audio}>", parsedPrompt);
    }

    [Fact]
    public void PreparsePromptLikes_RewritesAceStepFunTagWithAceStepSectionId()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        T2IParamInput input = new(null);
        input.Set(T2IParamTypes.Prompt, "global <acestepfun[1]>chorus lyrics");
        input.PreparsePromptLikes();

        string parsedPrompt = input.Get(T2IParamTypes.Prompt, "");
        Assert.Contains($"<acestepfun//cid={AceStepFunExtension.AceStepSectionIdForTrack(1)}>", parsedPrompt);
    }

    [Fact]
    public void GetTracks_AppliesAceStepSectionParamOverridesOnlyToMatchingTrack()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input);
        input.Set(T2IParamTypes.Prompt,
            "global"
            + $" <acestepfun><param[{AceStepFunExtension.Style.Type.ID}]:shared style><param[{AceStepFunExtension.Steps.Type.ID}]:9>shared prompt"
            + $" <acestepfun[1]><param[{AceStepFunExtension.Style.Type.ID}]:track one style><param[{AceStepFunExtension.AudioCfg.Type.ID}]:4.5><param[{AceStepFunExtension.Duration.Type.ID}]:33>track one prompt");
        input.Set(AceStepFunExtension.MusicTracks, new JArray(new JObject(), new JObject()).ToString());
        input.PreparsePromptLikes();

        List<Dictionary<string, object>> tracks = InvokeGetTracks(audioWorkflow);

        Assert.Equal(3, tracks.Count);
        Assert.Equal(0, tracks[0]["Index"]);
        Assert.Equal(1, tracks[1]["Index"]);
        Assert.Equal(2, tracks[2]["Index"]);
        Assert.Equal("shared style", tracks[0]["Style"]);
        Assert.Equal(9, tracks[0]["Steps"]);
        Assert.Equal(1.0, tracks[0]["AudioCfg"]);

        Assert.Equal("track one style", tracks[1]["Style"]);
        Assert.Equal(9, tracks[1]["Steps"]);
        Assert.Equal(4.5, tracks[1]["AudioCfg"]);
        Assert.Equal(33.0, tracks[1]["Duration"]);

        Assert.Equal("shared style", tracks[2]["Style"]);
        Assert.Equal(9, tracks[2]["Steps"]);
        Assert.Equal(1.0, tracks[2]["AudioCfg"]);
        Assert.Equal(120.0, tracks[2]["Duration"]);
    }

    [Fact]
    public void OnPreInit_RegistersFrontendAudioScript()
    {
        AceStepFunExtension extension = new();

        extension.OnPreInit();

        Assert.Contains("Assets/acestepfun.js", extension.ScriptFiles);
    }

    [Fact]
    public void IsExtensionActive_IsTrue_ForAceStepMainModelWithAudioSectionOnly()
    {
        EnsureParamsRegistered();
        EnsureAudioPromptRegistration();

        T2IParamInput input = new(null);
        input.Set(T2IParamTypes.Model, FakeAceStepModel());
        input.Set(T2IParamTypes.Prompt, "global setup <audio>section prompt");
        input.PreparsePromptLikes();

        WorkflowGenerator generator = new()
        {
            UserInput = input,
            Workflow = new JObject()
        };

        Runner runner = new(generator);
        Assert.True(InvokeIsExtensionActive(runner));
    }

    [Fact]
    public void IsExtensionActive_IsTrue_ForAceStepMainModelWithAudioLoraOnly()
    {
        EnsureParamsRegistered();

        T2IParamInput input = new(null);
        input.Set(T2IParamTypes.Model, FakeAceStepModel());
        input.Set(T2IParamTypes.Loras, ["audio-lora"]);
        input.Set(T2IParamTypes.LoraWeights, ["0.8"]);
        input.Set(T2IParamTypes.LoraSectionConfinement, [$"{AceStepFunExtension.SectionID_Audio}"]);

        WorkflowGenerator generator = new()
        {
            UserInput = input,
            Workflow = new JObject()
        };

        Runner runner = new(generator);
        Assert.True(InvokeIsExtensionActive(runner));
    }

    [Fact]
    public void IsExtensionActive_IsTrue_ForAceStepMainModelWithMusicTracksOnly()
    {
        EnsureParamsRegistered();

        T2IParamInput input = new(null);
        input.Set(T2IParamTypes.Model, FakeAceStepModel());
        input.Set(AceStepFunExtension.MusicTracks, new JArray(
            new JObject
            {
                ["Style"] = "layered synth",
                ["Duration"] = 60.0
            }
        ).ToString());

        WorkflowGenerator generator = new()
        {
            UserInput = input,
            Workflow = new JObject()
        };

        Runner runner = new(generator);
        Assert.True(InvokeIsExtensionActive(runner));
    }

    [Fact]
    public void ApplyConfiguredLoras_AddsModelOnlyLoaders_ForGlobalAndAudioPromptLoras()
    {
        EnsureParamsRegistered();
        EnsureFakeLoraModel("global-lora");
        EnsureFakeLoraModel("audio-lora");

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input, out WorkflowGenerator generator);
        input.Set(T2IParamTypes.Loras, ["global-lora", "audio-lora"]);
        input.Set(T2IParamTypes.LoraWeights, ["0.75", "0.5"]);
        input.Set(T2IParamTypes.LoraSectionConfinement, ["0", $"{AceStepFunExtension.SectionID_Audio}"]);
        generator.Workflow["4"] = new JObject
        {
            ["class_type"] = "UNETLoader",
            ["inputs"] = new JObject
            {
                ["unet_name"] = "audio/acestep_v1.5_merge_sft_turbo_ta_0.5",
                ["weight_dtype"] = "default"
            }
        };

        JArray resultPath = InvokeApplyConfiguredLoras(audioWorkflow, new JArray("4", 0));

        JObject audioLoraNode = Assert.IsType<JObject>(generator.Workflow[$"{resultPath[0]}"]);
        Assert.Equal("LoraLoaderModelOnly", $"{audioLoraNode["class_type"]}");
        JObject audioLoraInputs = Assert.IsType<JObject>(audioLoraNode["inputs"]);
        Assert.Equal("audio-lora.safetensors", $"{audioLoraInputs["lora_name"]}");
        Assert.Equal(0.5, audioLoraInputs["strength_model"]?.Value<double>());

        JArray previousModelPath = Assert.IsType<JArray>(audioLoraInputs["model"]);
        JObject globalLoraNode = Assert.IsType<JObject>(generator.Workflow[$"{previousModelPath[0]}"]);
        Assert.Equal("LoraLoaderModelOnly", $"{globalLoraNode["class_type"]}");
        JObject globalLoraInputs = Assert.IsType<JObject>(globalLoraNode["inputs"]);
        Assert.Equal("global-lora.safetensors", $"{globalLoraInputs["lora_name"]}");
        Assert.Equal(0.75, globalLoraInputs["strength_model"]?.Value<double>());
    }

    [Fact]
    public void ApplyConfiguredLoras_UsesOnlyGlobalAndMatchingAceStepTrackLoras()
    {
        EnsureParamsRegistered();
        EnsureFakeLoraModel("global-lora");
        EnsureFakeLoraModel("track-one-lora");
        EnsureFakeLoraModel("track-two-lora");

        object audioWorkflow = CreateAudioWorkflow(out T2IParamInput input, out WorkflowGenerator generator);
        input.Set(T2IParamTypes.Loras, ["global-lora", "track-one-lora", "track-two-lora"]);
        input.Set(T2IParamTypes.LoraWeights, ["0.75", "0.5", "0.25"]);
        input.Set(T2IParamTypes.LoraSectionConfinement, [
            "0",
            $"{AceStepFunExtension.AceStepSectionIdForTrack(1)}",
            $"{AceStepFunExtension.AceStepSectionIdForTrack(2)}"
        ]);
        generator.Workflow["4"] = new JObject
        {
            ["class_type"] = "UNETLoader",
            ["inputs"] = new JObject
            {
                ["unet_name"] = "audio/acestep_v1.5_merge_sft_turbo_ta_0.5",
                ["weight_dtype"] = "default"
            }
        };

        JArray resultPath = InvokeApplyConfiguredLoras(audioWorkflow, new JArray("4", 0), 1);

        JObject trackLoraNode = Assert.IsType<JObject>(generator.Workflow[$"{resultPath[0]}"]);
        Assert.Equal("LoraLoaderModelOnly", $"{trackLoraNode["class_type"]}");
        JObject trackLoraInputs = Assert.IsType<JObject>(trackLoraNode["inputs"]);
        Assert.Equal("track-one-lora.safetensors", $"{trackLoraInputs["lora_name"]}");
        Assert.Equal(0.5, trackLoraInputs["strength_model"]?.Value<double>());

        JArray previousModelPath = Assert.IsType<JArray>(trackLoraInputs["model"]);
        JObject globalLoraNode = Assert.IsType<JObject>(generator.Workflow[$"{previousModelPath[0]}"]);
        Assert.Equal("LoraLoaderModelOnly", $"{globalLoraNode["class_type"]}");
        JObject globalLoraInputs = Assert.IsType<JObject>(globalLoraNode["inputs"]);
        Assert.Equal("global-lora.safetensors", $"{globalLoraInputs["lora_name"]}");
        Assert.Equal(0.75, globalLoraInputs["strength_model"]?.Value<double>());

        Assert.DoesNotContain(generator.Workflow.Properties(), prop =>
            prop.Value is JObject node
            && node["inputs"] is JObject inputs
            && $"{inputs["lora_name"]}" == "track-two-lora.safetensors");
    }

    [Fact]
    public void TryPatchExistingText2AudioGraph_ReplacesGenericLoraLoader_WithModelOnlyLoader()
    {
        EnsureFakeLoraModel("global-lora");

        T2IParamInput input = new(null);
        input.Set(T2IParamTypes.Loras, ["global-lora"]);
        input.Set(T2IParamTypes.LoraWeights, ["0.8"]);
        input.Set(T2IParamTypes.LoraSectionConfinement, ["0"]);

        WorkflowGenerator generator = new()
        {
            UserInput = input,
            Workflow = BuildPatchableWorkflowWithGenericLora()
        };

        object audioWorkflow = CreateAudioWorkflow(generator);
        SetAudioWorkflowParams(audioWorkflow, [PatchedTrack()]);

        bool patched = InvokeTryPatchExistingText2AudioGraph(audioWorkflow);

        Assert.True(patched);

        JObject samplerNode = Assert.IsType<JObject>(generator.Workflow["5"]);
        JObject samplerInputs = Assert.IsType<JObject>(samplerNode["inputs"]);
        JArray modelPath = Assert.IsType<JArray>(samplerInputs["model"]);
        JObject auraNode = Assert.IsType<JObject>(generator.Workflow[$"{modelPath[0]}"]);
        JObject auraInputs = Assert.IsType<JObject>(auraNode["inputs"]);
        JArray loraModelPath = Assert.IsType<JArray>(auraInputs["model"]);
        JObject loraNode = Assert.IsType<JObject>(generator.Workflow[$"{loraModelPath[0]}"]);
        Assert.Equal("LoraLoaderModelOnly", $"{loraNode["class_type"]}");
        JObject loraInputs = Assert.IsType<JObject>(loraNode["inputs"]);
        Assert.Equal("global-lora.safetensors", $"{loraInputs["lora_name"]}");
        Assert.Equal(0.8, loraInputs["strength_model"]?.Value<double>());

        JObject positiveEncodeNode = Assert.IsType<JObject>(generator.Workflow["2"]);
        JObject positiveEncodeInputs = Assert.IsType<JObject>(positiveEncodeNode["inputs"]);
        JArray clipPath = Assert.IsType<JArray>(positiveEncodeInputs["clip"]);
        Assert.Equal("1", $"{clipPath[0]}");
    }

    [Fact]
    public void TryPatchExistingText2AudioGraph_CreatesSeparateOutputBranch_ForAdditionalTrack()
    {
        WorkflowGenerator generator = new()
        {
            UserInput = new T2IParamInput(null),
            Workflow = BuildPatchableWorkflow()
        };

        object audioWorkflow = CreateAudioWorkflow(generator);

        SetAudioWorkflowParams(audioWorkflow, [
            PatchedTrack(),
            (1, "layered lyrics", "layered tags", 22.0, 140L, "6", "ja", "C major")
        ]);

        bool patched = InvokeTryPatchExistingText2AudioGraph(audioWorkflow);

        Assert.True(patched);

        JObject samplerNode = Assert.IsType<JObject>(generator.Workflow["5"]);
        JObject samplerInputs = Assert.IsType<JObject>(samplerNode["inputs"]);
        JArray positivePath = Assert.IsType<JArray>(samplerInputs["positive"]);
        Assert.Equal("2", $"{positivePath[0]}");
        Assert.DoesNotContain(generator.Workflow.Properties(), prop => $"{((JObject)prop.Value)["class_type"]}" == "ConditioningCombine");

        JObject rootLatentNode = Assert.IsType<JObject>(generator.Workflow["8"]);
        JObject rootLatentInputs = Assert.IsType<JObject>(rootLatentNode["inputs"]);
        Assert.Equal(15.0, rootLatentInputs["seconds"]?.Value<double>());

        JProperty extraSaveProp = Assert.Single(
            generator.Workflow.Properties(),
            prop => $"{((JObject)prop.Value)["class_type"]}" == "SaveAudioMP3"
        );
        JObject extraSaveNode = Assert.IsType<JObject>(extraSaveProp.Value);
        JObject extraSaveInputs = Assert.IsType<JObject>(extraSaveNode["inputs"]);
        Assert.Equal("SwarmUI_track_2_", $"{extraSaveInputs["filename_prefix"]}");

        JArray extraAudioPath = Assert.IsType<JArray>(extraSaveInputs["audio"]);
        JObject extraDecodeNode = Assert.IsType<JObject>(generator.Workflow[$"{extraAudioPath[0]}"]);
        Assert.Equal("VAEDecodeAudio", $"{extraDecodeNode["class_type"]}");
        JObject extraDecodeInputs = Assert.IsType<JObject>(extraDecodeNode["inputs"]);

        JArray extraSamplerPath = Assert.IsType<JArray>(extraDecodeInputs["samples"]);
        Assert.NotEqual("5", $"{extraSamplerPath[0]}");
        JObject extraSamplerNode = Assert.IsType<JObject>(generator.Workflow[$"{extraSamplerPath[0]}"]);
        Assert.Equal("SwarmKSampler", $"{extraSamplerNode["class_type"]}");
        JObject extraSamplerInputs = Assert.IsType<JObject>(extraSamplerNode["inputs"]);

        JArray extraConditioningPath = Assert.IsType<JArray>(extraSamplerInputs["positive"]);
        JObject extraEncodeNode = Assert.IsType<JObject>(generator.Workflow[$"{extraConditioningPath[0]}"]);
        Assert.Equal("TextEncodeAceStepAudio1.5", $"{extraEncodeNode["class_type"]}");
        JObject extraEncodeInputs = Assert.IsType<JObject>(extraEncodeNode["inputs"]);
        Assert.Equal("layered lyrics", $"{extraEncodeInputs["lyrics"]}");
        Assert.Equal("layered tags", $"{extraEncodeInputs["tags"]}");
        Assert.Equal(22.0, extraEncodeInputs["duration"]?.Value<double>());
        Assert.Equal(140L, extraEncodeInputs["bpm"]?.Value<long>());
        Assert.Equal("6", $"{extraEncodeInputs["timesignature"]}");
        Assert.Equal("ja", $"{extraEncodeInputs["language"]}");
        Assert.Equal("C major", $"{extraEncodeInputs["keyscale"]}");

        JArray extraLatentPath = Assert.IsType<JArray>(extraSamplerInputs["latent_image"]);
        JObject extraLatentNode = Assert.IsType<JObject>(generator.Workflow[$"{extraLatentPath[0]}"]);
        JObject extraLatentInputs = Assert.IsType<JObject>(extraLatentNode["inputs"]);
        Assert.Equal(22.0, extraLatentInputs["seconds"]?.Value<double>());
    }

    [Fact]
    public void TryPatchExistingText2AudioGraph_DoesNotThrow_WhenAuraNodeMustBeInserted()
    {
        WorkflowGenerator generator = new()
        {
            UserInput = new T2IParamInput(null),
            Workflow = BuildPatchableWorkflow()
        };

        object audioWorkflow = CreateAudioWorkflow(generator);
        SetAudioWorkflowParams(audioWorkflow, [PatchedTrack()]);

        bool patched = InvokeTryPatchExistingText2AudioGraph(audioWorkflow);

        Assert.True(patched);

        JObject samplerNode = Assert.IsType<JObject>(generator.Workflow["5"]);
        Assert.Equal("SwarmKSampler", $"{samplerNode["class_type"]}");
        JObject samplerInputs = Assert.IsType<JObject>(samplerNode["inputs"]);

        JArray modelPath = Assert.IsType<JArray>(samplerInputs["model"]);
        JObject auraNode = Assert.IsType<JObject>(generator.Workflow[$"{modelPath[0]}"]);
        Assert.Equal("ModelSamplingAuraFlow", $"{auraNode["class_type"]}");

        JArray negativePath = Assert.IsType<JArray>(samplerInputs["negative"]);
        JObject zeroedNegativeNode = Assert.IsType<JObject>(generator.Workflow[$"{negativePath[0]}"]);
        Assert.Equal("ConditioningZeroOut", $"{zeroedNegativeNode["class_type"]}");
        JObject zeroedNegativeInputs = Assert.IsType<JObject>(zeroedNegativeNode["inputs"]);
        JArray zeroedConditioningPath = Assert.IsType<JArray>(zeroedNegativeInputs["conditioning"]);
        JObject negativeEncodeNode = Assert.IsType<JObject>(generator.Workflow[$"{zeroedConditioningPath[0]}"]);
        JObject negativeEncodeInputs = Assert.IsType<JObject>(negativeEncodeNode["inputs"]);
        Assert.Equal("patched lyrics", $"{negativeEncodeInputs["lyrics"]}");
        Assert.Equal("patched tags", $"{negativeEncodeInputs["tags"]}");

        JObject positiveEncodeNode = Assert.IsType<JObject>(generator.Workflow["2"]);
        JObject positiveEncodeInputs = Assert.IsType<JObject>(positiveEncodeNode["inputs"]);
        Assert.Equal("patched lyrics", $"{positiveEncodeInputs["lyrics"]}");
        Assert.Equal("patched tags", $"{positiveEncodeInputs["tags"]}");

        JObject clipLoaderNode = Assert.IsType<JObject>(generator.Workflow["1"]);
        JObject clipLoaderInputs = Assert.IsType<JObject>(clipLoaderNode["inputs"]);
        Assert.Equal("AceStep/qwen_0.6b_ace15.safetensors", $"{clipLoaderInputs["clip_name1"]}");
        Assert.Equal("AceStep/qwen_1.7b_ace15.safetensors", $"{clipLoaderInputs["clip_name2"]}");
    }

    private static bool InvokeTryPatchExistingText2AudioGraph(object audioWorkflow)
    {
        MethodInfo patchMethod = audioWorkflow.GetType().GetMethod("TryPatchExistingText2AudioGraph", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find TryPatchExistingText2AudioGraph.");
        try
        {
            object result = patchMethod.Invoke(audioWorkflow, []);
            return Assert.IsType<bool>(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static bool InvokeIsExtensionActive(Runner runner)
    {
        MethodInfo isExtensionActive = typeof(Runner).GetMethod("IsExtensionActive", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Runner.IsExtensionActive.");
        object result = isExtensionActive.Invoke(runner, []);
        return Assert.IsType<bool>(result);
    }

    private static JArray InvokeApplyConfiguredLoras(object audioWorkflow, JArray modelPath)
    {
        MethodInfo applyConfiguredLoras = audioWorkflow.GetType().GetMethod("ApplyConfiguredLoras", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(JArray)], null)
            ?? throw new InvalidOperationException("Could not find ApplyConfiguredLoras.");
        try
        {
            object result = applyConfiguredLoras.Invoke(audioWorkflow, [modelPath]);
            return Assert.IsType<JArray>(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static JArray InvokeApplyConfiguredLoras(object audioWorkflow, JArray modelPath, int trackIndex)
    {
        MethodInfo applyConfiguredLoras = audioWorkflow.GetType().GetMethod("ApplyConfiguredLoras", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(JArray), typeof(int)], null)
            ?? throw new InvalidOperationException("Could not find ApplyConfiguredLoras.");
        try
        {
            object result = applyConfiguredLoras.Invoke(audioWorkflow, [modelPath, trackIndex]);
            return Assert.IsType<JArray>(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static string InvokeResolvePrompt(object audioWorkflow)
    {
        MethodInfo resolvePrompt = audioWorkflow.GetType().GetMethod("ResolvePrompt", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
            ?? throw new InvalidOperationException("Could not find ResolvePrompt.");
        try
        {
            object result = resolvePrompt.Invoke(audioWorkflow, []);
            return Assert.IsType<string>(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static string InvokeResolvePrompt(object audioWorkflow, int trackIndex)
    {
        MethodInfo resolvePrompt = audioWorkflow.GetType().GetMethod("ResolvePrompt", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null)
            ?? throw new InvalidOperationException("Could not find indexed ResolvePrompt.");
        try
        {
            object result = resolvePrompt.Invoke(audioWorkflow, [trackIndex]);
            return Assert.IsType<string>(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static List<Dictionary<string, object>> InvokeGetTracks(object audioWorkflow)
    {
        MethodInfo getTracks = audioWorkflow.GetType().GetMethod("GetTracks", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find GetTracks.");
        try
        {
            object result = getTracks.Invoke(audioWorkflow, []);
            IEnumerable tracks = Assert.IsAssignableFrom<IEnumerable>(result);
            List<Dictionary<string, object>> parsedTracks = [];
            foreach (object track in tracks)
            {
                Dictionary<string, object> values = [];
                foreach (PropertyInfo property in track.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    values[property.Name] = property.GetValue(track);
                }
                parsedTracks.Add(values);
            }
            return parsedTracks;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void SetAudioWorkflowParams(
        object audioWorkflow,
        IReadOnlyList<(int Index, string Prompt, string Style, double Duration, long Bpm, string TimeSignature, string Language, string KeyScale)> tracks)
    {
        Type awType = audioWorkflow.GetType();
        Type trackType = typeof(JsonParser.TrackSpec);
        Type trackListType = typeof(List<>).MakeGenericType(trackType);
        IList trackList = Activator.CreateInstance(trackListType) as IList
            ?? throw new InvalidOperationException("Could not create track list.");

        foreach ((int index, string prompt, string style, double duration, long bpm, string timeSignature, string language, string keyScale) in tracks)
        {
            object trackInstance = Activator.CreateInstance(trackType, [
                index,
                prompt,
                style,
                duration,
                bpm,
                timeSignature,
                language,
                keyScale,
                "AceStep/qwen_1.7b_ace15.safetensors",
                2.0,
                2.0,
                8,
                3.0,
                "euler",
                "simple"
            ]) ?? throw new InvalidOperationException("Could not create TrackSpec.");
            trackList.Add(trackInstance);
        }

        Type paramsType = awType.GetNestedType("AudioParams", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find AudioParams type.");
        object paramsInstance = Activator.CreateInstance(paramsType, [
            trackList,
            "AceStep/qwen_0.6b_ace15.safetensors",
            "AceStep/qwen_1.7b_ace15.safetensors",
            123L,
            133L,
            2.0,
            2.0,
            8,
            "euler",
            "simple",
            3.0
        ]) ?? throw new InvalidOperationException("Could not create AudioParams.");
        FieldInfo paramsField = awType.GetField("Params", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Params field.");
        paramsField.SetValue(audioWorkflow, paramsInstance);
    }

    private static (int Index, string Prompt, string Style, double Duration, long Bpm, string TimeSignature, string Language, string KeyScale) PatchedTrack()
    {
        return (0, "patched lyrics", "patched tags", 15.0, 160L, "4", "en", "E minor");
    }

    private static object CreateAudioWorkflow(out T2IParamInput input)
    {
        return CreateAudioWorkflow(out input, out _);
    }

    private static object CreateAudioWorkflow(out T2IParamInput input, out WorkflowGenerator generator)
    {
        input = new T2IParamInput(null);
        generator = new WorkflowGenerator()
        {
            UserInput = input,
            Workflow = new JObject()
        };
        return CreateAudioWorkflow(generator);
    }

    private static object CreateAudioWorkflow(WorkflowGenerator generator)
    {
        Type audioWorkflowType = typeof(AceStepFunExtension).Assembly.GetType("AceStepFun.AudioWorkflow")
            ?? throw new InvalidOperationException("Could not find AceStepFun.AudioWorkflow.");
        return Activator.CreateInstance(audioWorkflowType, generator)
            ?? throw new InvalidOperationException("Could not create AceStepFun.AudioWorkflow.");
    }

    private static object CreateJsonParser(T2IParamInput input)
    {
        WorkflowGenerator generator = new()
        {
            UserInput = input,
            Workflow = new JObject()
        };
        return new JsonParser(generator);
    }

    private static T InvokeGetUserParam<T>(object jsonParser, T2IRegisteredParam<T> primary, T2IRegisteredParam<T> fallback)
    {
        MethodInfo getUserParam = jsonParser.GetType().GetMethod("GetUserParam", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find GetUserParam.");
        MethodInfo generic = getUserParam.MakeGenericMethod(typeof(T));
        try
        {
            object result = generic.Invoke(jsonParser, [primary, fallback]);
            return Assert.IsType<T>(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void EnsureAudioPromptRegistration()
    {
        MethodInfo onPreInit = typeof(AceStepFunExtension).GetMethod("OnPreInit", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Could not find AceStepFunExtension.OnPreInit.");
        AceStepFunExtension extension = new();
        onPreInit.Invoke(extension, []);
    }

    private static void EnsureParamsRegistered()
    {
        if (T2IParamTypes.Prompt is null)
        {
            T2IParamTypes.RegisterDefaults();
        }
        if (AceStepFunExtension.Prompt is not null)
        {
            return;
        }
        MethodInfo registerParameters = typeof(AceStepFunExtension).GetMethod("RegisterParameters", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find AceStepFunExtension.RegisterParameters.");
        registerParameters.Invoke(null, []);
    }

    private static void EnsureFakeLoraModel(string name)
    {
        if (!Program.T2IModelSets.TryGetValue("LoRA", out T2IModelHandler handler))
        {
            handler = new T2IModelHandler()
            {
                ModelType = "LoRA"
            };
            Program.T2IModelSets["LoRA"] = handler;
        }

        string fileName = $"{name}.safetensors";
        handler.Models[fileName] = new T2IModel(handler, "/fake/loras", $"/fake/loras/{fileName}", fileName);
    }

    private static T2IModel FakeAceStepModel()
    {
        return new T2IModel(null, "", "", "unit-test-ace-step")
        {
            ModelClass = new T2IModelClass
            {
                ID = "test-ace-step-1_5",
                Name = "unit-test-ace-step",
                CompatClass = T2IModelClassSorter.CompatAceStep15
            }
        };
    }

    private static JObject BuildPatchableWorkflowWithGenericLora()
    {
        JObject workflow = BuildPatchableWorkflow();
        workflow["2"] = new JObject
        {
            ["class_type"] = "TextEncodeAceStepAudio1.5",
            ["inputs"] = new JObject
            {
                ["clip"] = new JArray("9", 1),
                ["lyrics"] = "old lyrics",
                ["tags"] = "old tags",
                ["seed"] = 1,
                ["bpm"] = 120,
                ["duration"] = 10,
                ["timesignature"] = "4",
                ["language"] = "en",
                ["keyscale"] = "C major",
                ["generate_audio_codes"] = true,
                ["cfg_scale"] = 2.0
            }
        };
        workflow["3"] = new JObject
        {
            ["class_type"] = "TextEncodeAceStepAudio1.5",
            ["inputs"] = new JObject
            {
                ["clip"] = new JArray("9", 1),
                ["lyrics"] = "",
                ["tags"] = "",
                ["seed"] = 1,
                ["bpm"] = 120,
                ["duration"] = 10,
                ["timesignature"] = "4",
                ["language"] = "en",
                ["keyscale"] = "C major",
                ["generate_audio_codes"] = true,
                ["cfg_scale"] = 2.0
            }
        };
        workflow["9"] = new JObject
        {
            ["class_type"] = "LoraLoader",
            ["inputs"] = new JObject
            {
                ["model"] = new JArray("4", 0),
                ["clip"] = new JArray("1", 0),
                ["lora_name"] = "global-lora.safetensors",
                ["strength_model"] = 0.8,
                ["strength_clip"] = 0.8
            }
        };
        workflow["5"] = new JObject
        {
            ["class_type"] = "SwarmKSampler",
            ["inputs"] = new JObject
            {
                ["model"] = new JArray("9", 0),
                ["positive"] = new JArray("2", 0),
                ["negative"] = new JArray("3", 0),
                ["latent_image"] = new JArray("8", 0),
                ["seed"] = 5,
                ["steps"] = 20,
                ["cfg"] = 7,
                ["sampler_name"] = "euler",
                ["scheduler"] = "normal",
                ["start_at_step"] = 0,
                ["end_at_step"] = 10000,
                ["return_with_leftover_noise"] = "disable",
                ["add_noise"] = "enable",
                ["var_seed"] = 0,
                ["var_seed_strength"] = 0,
                ["sigma_min"] = -1,
                ["sigma_max"] = -1,
                ["rho"] = 7,
                ["previews"] = "default",
                ["tile_sample"] = false,
                ["tile_size"] = 768
            }
        };
        return workflow;
    }

    private static JObject BuildPatchableWorkflow()
    {
        return new JObject
        {
            ["1"] = new JObject
            {
                ["class_type"] = "DualCLIPLoader",
                ["inputs"] = new JObject
                {
                    ["clip_name1"] = "old-clip-1.safetensors",
                    ["clip_name2"] = "old-clip-2.safetensors",
                    ["type"] = "sd",
                    ["device"] = "cpu"
                }
            },
            ["2"] = new JObject
            {
                ["class_type"] = "TextEncodeAceStepAudio1.5",
                ["inputs"] = new JObject
                {
                    ["clip"] = new JArray("1", 0),
                    ["lyrics"] = "old lyrics",
                    ["tags"] = "old tags",
                    ["seed"] = 1,
                    ["bpm"] = 120,
                    ["duration"] = 10,
                    ["timesignature"] = "4",
                    ["language"] = "en",
                    ["keyscale"] = "C major",
                    ["generate_audio_codes"] = true,
                    ["cfg_scale"] = 2.0
                }
            },
            ["3"] = new JObject
            {
                ["class_type"] = "TextEncodeAceStepAudio1.5",
                ["inputs"] = new JObject
                {
                    ["clip"] = new JArray("1", 0),
                    ["lyrics"] = "",
                    ["tags"] = "",
                    ["seed"] = 1,
                    ["bpm"] = 120,
                    ["duration"] = 10,
                    ["timesignature"] = "4",
                    ["language"] = "en",
                    ["keyscale"] = "C major",
                    ["generate_audio_codes"] = true,
                    ["cfg_scale"] = 2.0
                }
            },
            ["4"] = new JObject
            {
                ["class_type"] = "UNetLoader",
                ["inputs"] = new JObject
                {
                    ["unet_name"] = "audio/acestep_v1.5_merge_sft_turbo_ta_0.5",
                    ["weight_dtype"] = "default"
                }
            },
            ["5"] = new JObject
            {
                ["class_type"] = "SwarmKSampler",
                ["inputs"] = new JObject
                {
                    ["model"] = new JArray("4", 0),
                    ["positive"] = new JArray("2", 0),
                    ["negative"] = new JArray("3", 0),
                    ["latent_image"] = new JArray("8", 0),
                    ["seed"] = 5,
                    ["steps"] = 20,
                    ["cfg"] = 7,
                    ["sampler_name"] = "euler",
                    ["scheduler"] = "normal",
                    ["start_at_step"] = 0,
                    ["end_at_step"] = 10000,
                    ["return_with_leftover_noise"] = "disable",
                    ["add_noise"] = "enable",
                    ["var_seed"] = 0,
                    ["var_seed_strength"] = 0,
                    ["sigma_min"] = -1,
                    ["sigma_max"] = -1,
                    ["rho"] = 7,
                    ["previews"] = "default",
                    ["tile_sample"] = false,
                    ["tile_size"] = 768
                }
            },
            ["6"] = new JObject
            {
                ["class_type"] = "VAEDecodeAudio",
                ["inputs"] = new JObject
                {
                    ["samples"] = new JArray("5", 0),
                    ["vae"] = new JArray("7", 0)
                }
            },
            ["7"] = new JObject
            {
                ["class_type"] = "VAELoader",
                ["inputs"] = new JObject
                {
                    ["vae_name"] = "ace-step-15-vae"
                }
            },
            ["8"] = new JObject
            {
                ["class_type"] = "EmptyAceStep1.5LatentAudio",
                ["inputs"] = new JObject
                {
                    ["batch_size"] = 1,
                    ["seconds"] = 10
                }
            }
        };
    }
}
