using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace AceStepFun;

internal class AudioWorkflow(WorkflowGenerator g)
{
    private sealed record AudioParams(
        string Prompt,
        string Clip1,
        string Clip2,
        string Style,
        long Seed,
        long ConditioningSeed,
        double Duration,
        long Bpm,
        string TimeSignature,
        string Language,
        string KeyScale,
        double AudioCfgScale,
        double SamplerCfg,
        int Steps,
        string AudioSamplerName = "euler",
        string AudioScheduler = "simple",
        double AuraShift = 3
    );

    private const int AudioIdBase = 64100;
    private const string DefaultClip1 = "AceStep/qwen_0.6b_ace15.safetensors";
    private const string DefaultClip2 = "AceStep/qwen_1.7b_ace15.safetensors";
    private AudioParams Params;

    public void Run()
    {
        T2IModel aceModel = GetSelectedAceModel();
        if (aceModel is null)
        {
            return;
        }

        WorkflowGenerator.ModelLoadHelpers helpers = new(g);
        string clip2Selection = GetUserParam(
            AceStepFunExtension.LmModel,
            AceStepFunExtension.Text2AudioLmModel,
            DefaultClip2
        );
        (string clip1Name, string clip1Url, string clip1Hash) = GetClipInfo(DefaultClip1);
        (string clip2Name, string clip2Url, string clip2Hash) = GetClipInfo(clip2Selection);
        long seed = g.UserInput.Get(T2IParamTypes.Seed, 0);
        Params = new(
            Prompt: ResolvePrompt(),
            Clip1: helpers.RequireClipModel(clip1Name, clip1Url, clip1Hash, null),
            Clip2: helpers.RequireClipModel(clip2Name, clip2Url, clip2Hash, null),
            Style: GetUserParam(
                AceStepFunExtension.Style,
                T2IParamTypes.Text2AudioStyle,
                ""
            ),
            Seed: seed,
            ConditioningSeed: seed + 10,
            Duration: GetUserParam(
                AceStepFunExtension.Duration,
                T2IParamTypes.Text2AudioDuration,
                120.0
            ),
            Bpm: GetUserParam(
                AceStepFunExtension.Bpm,
                T2IParamTypes.Text2AudioBPM,
                120L
            ),
            TimeSignature: GetUserParam(
                AceStepFunExtension.TimeSignature,
                T2IParamTypes.Text2AudioTimeSignature,
                "4"
            ),
            Language: GetUserParam(
                AceStepFunExtension.Language,
                T2IParamTypes.Text2AudioLanguage,
                "en"
            ),
            KeyScale: GetUserParam(
                AceStepFunExtension.KeyScale,
                T2IParamTypes.Text2AudioKeyScale,
                "E minor"
            ),
            AudioCfgScale: GetUserParam(
                AceStepFunExtension.AudioCfg,
                AceStepFunExtension.Text2AudioAudioCfg,
                2.0
            ),
            SamplerCfg: GetUserParam(
                AceStepFunExtension.SamplerCfg,
                AceStepFunExtension.Text2AudioSamplerCfg,
                1.0
            ),
            Steps: (int)GetUserParam(
                AceStepFunExtension.Steps,
                AceStepFunExtension.Text2AudioSteps,
                20L
            )
        );

        bool mainModelIsAceStep = g.UserInput.TryGet(T2IParamTypes.Model, out T2IModel mainModel)
            && mainModel?.ModelClass?.CompatClass == T2IModelClassSorter.CompatAceStep15;

        if (TryPatchExistingText2AudioGraph() || mainModelIsAceStep)
        {
            return;
        }

        string clipNode = g.CreateNode(NodeTypes.DualClipLoader, new JObject
        {
            ["clip_name1"] = Params.Clip1,
            ["clip_name2"] = Params.Clip2,
            ["type"] = "ace",
            ["device"] = "default"
        }, g.GetStableDynamicID(AudioIdBase + 10, 0));

        string modelNode = g.CreateNode(NodeTypes.UNetLoader, new JObject
        {
            ["unet_name"] = aceModel.ToString(g.ModelFolderFormat),
            ["weight_dtype"] = "default"
        }, g.GetStableDynamicID(AudioIdBase + 20, 0));

        string samplingNode = g.CreateNode(NodeTypes.ModelSamplingAuraFlow, new JObject
        {
            ["model"] = new JArray(modelNode, 0),
            ["shift"] = Params.AuraShift
        }, g.GetStableDynamicID(AudioIdBase + 25, 0));

        JArray previousVae = g.LoadingVAE;
        helpers.DoVaeLoader(null, T2IModelClassSorter.CompatAceStep15, "ace-step-15-vae");
        JArray vaeNode = g.LoadingVAE;
        g.LoadingVAE = previousVae;

        string positivePromptNode = g.CreateNode(NodeTypes.TextEncodeAceStepAudio, new JObject
        {
            ["clip"] = new JArray(clipNode, 0),
            ["lyrics"] = Params.Prompt,
            ["tags"] = Params.Style,
            ["seed"] = Params.ConditioningSeed,
            ["bpm"] = Params.Bpm,
            ["duration"] = Params.Duration,
            ["timesignature"] = Params.TimeSignature,
            ["language"] = Params.Language,
            ["keyscale"] = Params.KeyScale,
            ["generate_audio_codes"] = true,
            ["cfg_scale"] = Params.AudioCfgScale,
            ["temperature"] = 0.85,
            ["top_p"] = 0.9,
            ["top_k"] = 0,
            ["min_p"] = 0
        }, g.GetStableDynamicID(AudioIdBase + 30, 0));

        string negativePromptNode = g.CreateNode(NodeTypes.TextEncodeAceStepAudio, new JObject
        {
            ["clip"] = new JArray(clipNode, 0),
            ["lyrics"] = "",
            ["tags"] = "",
            ["seed"] = Params.ConditioningSeed,
            ["bpm"] = Params.Bpm,
            ["duration"] = Params.Duration,
            ["timesignature"] = Params.TimeSignature,
            ["language"] = Params.Language,
            ["keyscale"] = Params.KeyScale,
            ["generate_audio_codes"] = true,
            ["cfg_scale"] = Params.AudioCfgScale,
            ["temperature"] = 0.85,
            ["top_p"] = 0.9,
            ["top_k"] = 0,
            ["min_p"] = 0
        }, g.GetStableDynamicID(AudioIdBase + 35, 0));

        string latentNode = g.CreateNode(NodeTypes.EmptyAceStepAudioLatent, new JObject
        {
            ["batch_size"] = g.UserInput.Get(T2IParamTypes.BatchSize, 1),
            ["seconds"] = Params.Duration
        }, g.GetStableDynamicID(AudioIdBase + 40, 0));

        string zeroedNegativeNode = g.CreateNode(NodeTypes.ConditioningZeroOut, new JObject
        {
            ["conditioning"] = new JArray(negativePromptNode, 0)
        }, g.GetStableDynamicID(AudioIdBase + 45, 0));

        string samplerNode = g.CreateNode(NodeTypes.SwarmKSampler, new JObject
        {
            ["model"] = new JArray(samplingNode, 0),
            ["noise_seed"] = Params.Seed,
            ["steps"] = Params.Steps,
            ["cfg"] = Params.SamplerCfg,
            ["sampler_name"] = Params.AudioSamplerName,
            ["scheduler"] = Params.AudioScheduler,
            ["positive"] = new JArray(positivePromptNode, 0),
            ["negative"] = new JArray(zeroedNegativeNode, 0),
            ["latent_image"] = new JArray(latentNode, 0),
            ["start_at_step"] = 0,
            ["end_at_step"] = 10000,
            ["return_with_leftover_noise"] = "disable",
            ["add_noise"] = "enable",
            ["var_seed"] = g.UserInput.Get(T2IParamTypes.VariationSeed, 0),
            ["var_seed_strength"] = g.UserInput.Get(T2IParamTypes.VariationSeedStrength, 0),
            ["sigma_min"] = g.UserInput.Get(T2IParamTypes.SamplerSigmaMin, -1),
            ["sigma_max"] = g.UserInput.Get(T2IParamTypes.SamplerSigmaMax, -1),
            ["rho"] = g.UserInput.Get(T2IParamTypes.SamplerRho, 7),
            ["previews"] = g.UserInput.Get(T2IParamTypes.NoPreviews) ? "none" : "default",
            ["tile_sample"] = false,
            ["tile_size"] = 768
        }, g.GetStableDynamicID(AudioIdBase + 50, 0));

        _ = g.CreateNode(NodeTypes.VAEDecodeAudio, new JObject
        {
            ["samples"] = new JArray(samplerNode, 0),
            ["vae"] = vaeNode
        }, g.GetStableDynamicID(AudioIdBase + 60, 0));
    }

    private bool TryPatchExistingText2AudioGraph()
    {
        HashSet<string> referencedClipNodeIds = [];
        HashSet<string> aceEncodeNodeIds = [];
        HashSet<string> aceLatentNodeIds = [];
        HashSet<string> samplerNodeIds = [];
        List<JObject> samplersNeedingZeroedNegative = [];
        int encodeNodeCount = 0;

        foreach (JProperty prop in g.Workflow.Properties().ToList())
        {
            if (prop.Value is not JObject node)
            {
                continue;
            }

            string classType = $"{node["class_type"]}";
            if (classType == NodeTypes.TextEncodeAceStepAudio
                && node["inputs"] is JObject encodeInputs)
            {
                if (encodeInputs.TryGetValue("clip", out JToken clipTok)
                    && clipTok is JArray clipPath
                    && clipPath.Count > 0)
                {
                    referencedClipNodeIds.Add($"{clipPath[0]}");
                }
                aceEncodeNodeIds.Add(prop.Name);
                bool isNegative = string.IsNullOrWhiteSpace($"{encodeInputs["lyrics"]}")
                    && string.IsNullOrWhiteSpace($"{encodeInputs["tags"]}");
                encodeInputs["seed"] = Params.ConditioningSeed;
                encodeInputs["bpm"] = Params.Bpm;
                encodeInputs["duration"] = Params.Duration;
                encodeInputs["timesignature"] = Params.TimeSignature;
                encodeInputs["language"] = Params.Language;
                encodeInputs["keyscale"] = Params.KeyScale;
                encodeInputs["generate_audio_codes"] = true;
                encodeInputs["cfg_scale"] = Params.AudioCfgScale;
                encodeInputs["temperature"] = 0.85;
                encodeInputs["top_p"] = 0.9;
                encodeInputs["top_k"] = 0;
                encodeInputs["min_p"] = 0;
                encodeInputs["lyrics"] = "";
                encodeInputs["tags"] = "";
                if (!isNegative)
                {
                    encodeInputs["lyrics"] = Params.Prompt;
                    encodeInputs["tags"] = Params.Style;
                }
                encodeNodeCount++;
            }
            else if (classType == NodeTypes.EmptyAceStepAudioLatent
                && node["inputs"] is JObject latentInputs)
            {
                latentInputs["seconds"] = Params.Duration;
                aceLatentNodeIds.Add(prop.Name);
            }
        }

        if (encodeNodeCount == 0)
        {
            return false;
        }

        foreach (JProperty prop in g.Workflow.Properties().ToList())
        {
            if (prop.Value is not JObject node)
            {
                continue;
            }
            string classType = $"{node["class_type"]}";
            if ((classType != NodeTypes.KSampler && classType != NodeTypes.SwarmKSampler)
                || node["inputs"] is not JObject samplerInputs)
            {
                continue;
            }
            if (!IsAceAudioSamplerNode(samplerInputs, aceEncodeNodeIds, aceLatentNodeIds))
            {
                continue;
            }
            samplerInputs["steps"] = Params.Steps;
            samplerInputs["cfg"] = Params.SamplerCfg;
            samplerInputs["sampler_name"] = Params.AudioSamplerName;
            samplerInputs["scheduler"] = Params.AudioScheduler;
            samplerInputs["model"] = EnsureAuraModelInput(samplerInputs);
            if (NeedsZeroedNegativeInput(samplerInputs))
            {
                samplersNeedingZeroedNegative.Add(samplerInputs);
            }
            if (classType == NodeTypes.KSampler)
            {
                node["class_type"] = NodeTypes.SwarmKSampler;
            }
            EnsureSwarmKSamplerInputs(samplerInputs);
            samplerNodeIds.Add(prop.Name);
        }

        if (samplerNodeIds.Count == 0)
        {
            return false;
        }

        foreach (JObject samplerInputs in samplersNeedingZeroedNegative)
        {
            EnsureZeroedNegativeInput(samplerInputs);
        }

        foreach (string clipNodeId in referencedClipNodeIds)
        {
            if (!g.Workflow.TryGetValue(clipNodeId, out JToken clipNodeTok)
                || clipNodeTok is not JObject clipNode)
            {
                continue;
            }
            if ($"{clipNode["class_type"]}" != NodeTypes.DualClipLoader
                || clipNode["inputs"] is not JObject clipInputs)
            {
                continue;
            }
            clipInputs["clip_name1"] = Params.Clip1;
            clipInputs["clip_name2"] = Params.Clip2;
            clipInputs["type"] = "ace";
            clipInputs["device"] = "default";
        }

        return HasAudioNodeForPatchedSamplers(samplerNodeIds);
    }

    private bool HasAudioNodeForPatchedSamplers(HashSet<string> samplerNodeIds)
    {
        foreach (JProperty prop in g.Workflow.Properties())
        {
            if (prop.Value is not JObject node
                || $"{node["class_type"]}" != NodeTypes.VAEDecodeAudio
                || node["inputs"] is not JObject inputs)
            {
                continue;
            }
            if (inputs.TryGetValue("samples", out JToken samplesTok)
                && samplesTok is JArray samplePath && samplePath.Count > 0
                && samplerNodeIds.Contains($"{samplePath[0]}"))
            {
                return true;
            }
        }
        return false;
    }

    private JArray EnsureAuraModelInput(JObject samplerInputs)
    {
        if (!samplerInputs.TryGetValue("model", out JToken modelTok)
            || modelTok is not JArray modelPath || modelPath.Count < 2)
        {
            return [];
        }
        string sourceNodeId = $"{modelPath[0]}";
        if (g.Workflow.TryGetValue(sourceNodeId, out JToken sourceTok)
            && sourceTok is JObject sourceNode
            && $"{sourceNode["class_type"]}" == NodeTypes.ModelSamplingAuraFlow)
        {
            if (sourceNode["inputs"] is JObject sourceInputs)
            {
                sourceInputs["shift"] = Params.AuraShift;
            }
            return modelPath;
        }
        string auraNode = g.CreateNode(NodeTypes.ModelSamplingAuraFlow, new JObject
        {
            ["model"] = modelPath,
            ["shift"] = Params.AuraShift
        });
        return new JArray(auraNode, 0);
    }

    private void EnsureZeroedNegativeInput(JObject samplerInputs)
    {
        if (!samplerInputs.TryGetValue("negative", out JToken negativeTok)
            || negativeTok is not JArray negativePath
            || negativePath.Count < 2)
        {
            return;
        }
        string sourceNodeId = $"{negativePath[0]}";
        if (g.Workflow.TryGetValue(sourceNodeId, out JToken sourceTok)
            && sourceTok is JObject sourceNode
            && $"{sourceNode["class_type"]}" == NodeTypes.ConditioningZeroOut)
        {
            return;
        }
        string zeroedId = g.CreateNode(NodeTypes.ConditioningZeroOut, new JObject()
        {
            ["conditioning"] = negativePath
        });
        samplerInputs["negative"] = new JArray(zeroedId, 0);
    }

    private bool NeedsZeroedNegativeInput(JObject samplerInputs)
    {
        if (!samplerInputs.TryGetValue("negative", out JToken negativeTok)
            || negativeTok is not JArray negativePath
            || negativePath.Count < 2)
        {
            return false;
        }
        string sourceNodeId = $"{negativePath[0]}";
        return !g.Workflow.TryGetValue(sourceNodeId, out JToken sourceTok)
            || sourceTok is not JObject sourceNode
            || $"{sourceNode["class_type"]}" != NodeTypes.ConditioningZeroOut;
    }

    private void EnsureSwarmKSamplerInputs(JObject samplerInputs)
    {
        if (!samplerInputs.ContainsKey("noise_seed")
            && samplerInputs.TryGetValue("seed", out JToken seedTok))
        {
            samplerInputs["noise_seed"] = seedTok;
        }
        if (!samplerInputs.ContainsKey("start_at_step"))
        {
            samplerInputs["start_at_step"] = 0;
        }
        if (!samplerInputs.ContainsKey("end_at_step"))
        {
            samplerInputs["end_at_step"] = 10000;
        }
        if (!samplerInputs.ContainsKey("return_with_leftover_noise"))
        {
            samplerInputs["return_with_leftover_noise"] = "disable";
        }
        if (!samplerInputs.ContainsKey("add_noise"))
        {
            samplerInputs["add_noise"] = "enable";
        }
        if (!samplerInputs.ContainsKey("var_seed"))
        {
            samplerInputs["var_seed"] = g.UserInput.Get(T2IParamTypes.VariationSeed, 0);
        }
        if (!samplerInputs.ContainsKey("var_seed_strength"))
        {
            samplerInputs["var_seed_strength"] = g.UserInput.Get(T2IParamTypes.VariationSeedStrength, 0);
        }
        if (!samplerInputs.ContainsKey("sigma_min"))
        {
            samplerInputs["sigma_min"] = g.UserInput.Get(T2IParamTypes.SamplerSigmaMin, -1);
        }
        if (!samplerInputs.ContainsKey("sigma_max"))
        {
            samplerInputs["sigma_max"] = g.UserInput.Get(T2IParamTypes.SamplerSigmaMax, -1);
        }
        if (!samplerInputs.ContainsKey("rho"))
        {
            samplerInputs["rho"] = g.UserInput.Get(T2IParamTypes.SamplerRho, 7);
        }
        if (!samplerInputs.ContainsKey("previews"))
        {
            samplerInputs["previews"] = g.UserInput.Get(T2IParamTypes.NoPreviews) ? "none" : "default";
        }
        if (!samplerInputs.ContainsKey("tile_sample"))
        {
            samplerInputs["tile_sample"] = false;
        }
        if (!samplerInputs.ContainsKey("tile_size"))
        {
            samplerInputs["tile_size"] = 768;
        }
        if (samplerInputs.ContainsKey("seed"))
        {
            samplerInputs.Remove("seed");
        }
    }

    private bool IsAceAudioSamplerNode(
        JObject samplerInputs,
        HashSet<string> aceEncodeNodeIds,
        HashSet<string> aceLatentNodeIds)
    {
        return IsInputFromNodeSet(samplerInputs, "latent_image", aceLatentNodeIds)
            || IsInputFromAceEncodeConditioning(samplerInputs, "positive", aceEncodeNodeIds)
            || IsInputFromAceEncodeConditioning(samplerInputs, "negative", aceEncodeNodeIds);
    }

    private bool IsInputFromAceEncodeConditioning(
        JObject inputs,
        string inputName,
        HashSet<string> aceEncodeNodeIds)
    {
        if (!TryGetInputSourceId(inputs, inputName, out string sourceNodeId))
        {
            return false;
        }
        if (aceEncodeNodeIds.Contains(sourceNodeId))
        {
            return true;
        }
        if (!g.Workflow.TryGetValue(sourceNodeId, out JToken sourceTok)
            || sourceTok is not JObject sourceNode
            || $"{sourceNode["class_type"]}" != NodeTypes.ConditioningZeroOut
            || sourceNode["inputs"] is not JObject sourceInputs)
        {
            return false;
        }
        return TryGetInputSourceId(sourceInputs, "conditioning", out string conditioningNodeId)
            && aceEncodeNodeIds.Contains(conditioningNodeId);
    }

    private string ResolvePrompt()
    {
        return PromptParser.ResolvePrompt(g.UserInput);
    }

    private static bool IsInputFromNodeSet(JObject inputs, string inputName, HashSet<string> nodeIds)
    {
        return TryGetInputSourceId(inputs, inputName, out string sourceNodeId) && nodeIds.Contains(sourceNodeId);
    }

    private static bool TryGetInputSourceId(JObject inputs, string inputName, out string sourceNodeId)
    {
        sourceNodeId = null;
        if (!inputs.TryGetValue(inputName, out JToken inputTok) || inputTok is not JArray path || path.Count < 1)
        {
            return false;
        }
        sourceNodeId = $"{path[0]}";
        return !string.IsNullOrWhiteSpace(sourceNodeId);
    }

    private T GetUserParam<T>(T2IRegisteredParam<T> primary, T2IRegisteredParam<T> fallback, T defaultValue)
    {
        bool hasPrimary = HasRaw(primary);
        bool hasFallback = HasRaw(fallback);
        T primaryValue = hasPrimary ? g.UserInput.Get(primary, defaultValue) : defaultValue;
        T fallbackValue = hasFallback ? g.UserInput.Get(fallback, defaultValue) : defaultValue;

        if (hasPrimary && !hasFallback)
        {
            return primaryValue;
        }
        if (!hasPrimary && hasFallback)
        {
            return fallbackValue;
        }
        if (hasPrimary && hasFallback)
        {
            // If primary is only default-valued while fallback is explicitly set, prefer fallback.
            if (EqualityComparer<T>.Default.Equals(primaryValue, defaultValue))
            {
                return fallbackValue;
            }
            return primaryValue;
        }
        return g.UserInput.Get(primary, defaultValue);
    }

    private bool HasRaw<T>(T2IRegisteredParam<T> param)
    {
        return param?.Type is not null && g.UserInput.TryGetRaw(param.Type, out _);
    }

    private T2IModel GetSelectedAceModel()
    {
        if (g.UserInput.TryGet(AceStepFunExtension.Model, out T2IModel aceModel) && aceModel is not null)
        {
            return aceModel;
        }
        if (g.UserInput.TryGet(T2IParamTypes.Model, out T2IModel model)
            && model?.ModelClass?.CompatClass == T2IModelClassSorter.CompatAceStep15)
        {
            return model;
        }
        return null;
    }

    private static (string Name, string Url, string Hash) GetClipInfo(string selectedName)
    {
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            selectedName = "";
        }
        selectedName = selectedName.ToLower().Trim();

        return selectedName switch
        {
            "acestep/qwen_0.6b_ace15.safetensors" => (
                "AceStep/qwen_0.6b_ace15.safetensors",
                "https://huggingface.co/Comfy-Org/ace_step_1.5_ComfyUI_files/resolve/main/split_files/text_encoders/qwen_0.6b_ace15.safetensors",
                "fd4590c82153b8ddb67e15a2e7aaa8afa8b83a858c8a9b82a4831063156aa7a7"
            ),
            "acestep/qwen_4b_ace15.safetensors" => (
                "AceStep/qwen_4b_ace15.safetensors",
                "https://huggingface.co/Comfy-Org/ace_step_1.5_ComfyUI_files/resolve/main/split_files/text_encoders/qwen_4b_ace15.safetensors",
                "ffe5ffb855086c2ab55e467e9859fb01894781020a0376484dd19de166b79873"
            ),
            _ => (
                "AceStep/qwen_1.7b_ace15.safetensors",
                "https://huggingface.co/Comfy-Org/ace_step_1.5_ComfyUI_files/resolve/main/split_files/text_encoders/qwen_1.7b_ace15.safetensors",
                "ed63e9247d1f55f3ace04fa11e95b085fc82d459c82c5626f0b2e37b91ebd710"
            ),
        };
    }
}
