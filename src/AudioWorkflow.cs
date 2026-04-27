using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace AceStepFun;

internal class AudioWorkflow(WorkflowGenerator g)
{
    private sealed record AudioParams(
        List<JsonParser.TrackSpec> Tracks,
        string Clip1,
        string Clip2,
        long Seed,
        long ConditioningSeed,
        double LmCfgScale,
        double AudioCfg,
        int Steps,
        string AudioSamplerName = "euler",
        string AudioScheduler = "simple",
        double SigmaShift = 3
    );

    private sealed record ExistingLoraChain(
        JArray RootModelPath,
        List<AceStepLora> Loras,
        bool RequiresNormalization
    );

    private sealed record PatchedSamplerOutput(
        int BranchIndex,
        JArray ModelPath,
        JArray VaePath
    );

    private const int AudioIdBase = 64100;
    private const string DefaultClip1 = "AceStep/qwen_0.6b_ace15.safetensors";
    private AudioParams Params;
    private WorkflowGenerator.ModelLoadHelpers Helpers;
    private Dictionary<string, JArray> ClipPathCache;

    public void Run()
    {
        T2IModel aceModel = GetSelectedAceModel();
        if (aceModel is null)
        {
            return;
        }

        Helpers = new(g);
        ClipPathCache = [];
        (string clip1Name, string clip1Url, string clip1Hash) = GetClipInfo(DefaultClip1);
        long seed = g.UserInput.Get(T2IParamTypes.Seed, 0);
        List<JsonParser.TrackSpec> tracks = GetTracks();
        string clip2Selection = tracks[0].LmModel;
        (string clip2Name, string clip2Url, string clip2Hash) = GetClipInfo(clip2Selection);
        Params = new(
            Tracks: tracks,
            Clip1: Helpers.RequireClipModel(clip1Name, clip1Url, clip1Hash, null),
            Clip2: Helpers.RequireClipModel(clip2Name, clip2Url, clip2Hash, null),
            Seed: seed,
            ConditioningSeed: seed + 10,
            LmCfgScale: tracks[0].LmCfgScale,
            AudioCfg: tracks[0].AudioCfg,
            Steps: tracks[0].Steps,
            SigmaShift: tracks[0].SigmaShift
        );

        bool mainModelIsAceStep = g.UserInput.TryGet(T2IParamTypes.Model, out T2IModel mainModel)
            && mainModel?.ModelClass?.CompatClass == T2IModelClassSorter.CompatAceStep15;

        if (TryPatchExistingText2AudioGraph() || mainModelIsAceStep)
        {
            return;
        }

        string modelNode = g.CreateNode(NodeTypes.UNetLoader, new JObject
        {
            ["unet_name"] = aceModel.ToString(g.ModelFolderFormat),
            ["weight_dtype"] = "default"
        }, g.GetStableDynamicID(AudioIdBase + 20, 0));

        JArray previousVae = g.LoadingVAE;
        Helpers.DoVaeLoader(null, T2IModelClassSorter.CompatAceStep15, "ace-step-15-vae");
        JArray vaeNode = g.LoadingVAE;
        g.LoadingVAE = previousVae;

        JArray baseModelPath = new(modelNode, 0);
        foreach (JsonParser.TrackSpec track in Params.Tracks)
        {
            JArray clipPath = CreateTrackClipPath(track, branchIndex: 0);
            JArray modelPathForSampling = CreateTrackSamplerModelPath(baseModelPath, track);
            _ = CreateTrackOutputBranch(clipPath, modelPathForSampling, vaeNode, track, branchIndex: 0);
        }
    }

    private bool TryPatchExistingText2AudioGraph()
    {
        HashSet<string> referencedClipNodeIds = [];
        HashSet<string> aceEncodeNodeIds = [];
        HashSet<string> aceLatentNodeIds = [];
        HashSet<string> samplerNodeIds = [];
        Dictionary<string, JArray> patchedSamplerBaseModels = [];
        List<JObject> samplersNeedingZeroedNegative = [];
        JArray sharedClipPath = null;
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
                    JArray normalizedClipPath = NormalizeClipPath(clipPath);
                    encodeInputs["clip"] = normalizedClipPath;
                    referencedClipNodeIds.Add($"{normalizedClipPath[0]}");
                    if (sharedClipPath is null)
                    {
                        sharedClipPath = new JArray(normalizedClipPath);
                    }
                }
                aceEncodeNodeIds.Add(prop.Name);
                ApplyTrackInputs(encodeInputs, Params.Tracks[0]);
                encodeNodeCount++;
            }
            else if (classType == NodeTypes.EmptyAceStepAudioLatent
                && node["inputs"] is JObject latentInputs)
            {
                latentInputs["seconds"] = Params.Tracks[0].Duration;
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
            samplerInputs["steps"] = Params.Tracks[0].Steps;
            samplerInputs["cfg"] = Params.Tracks[0].AudioCfg;
            samplerInputs["sampler_name"] = Params.Tracks[0].AudioSamplerName;
            samplerInputs["scheduler"] = Params.Tracks[0].AudioScheduler;
            JArray modelInput = GetInputPath(samplerInputs, "model");
            JArray baseModelInput = new(modelInput);
            modelInput = ApplyConfiguredLorasToSamplerModel(modelInput, Params.Tracks[0].Index);
            samplerInputs["model"] = EnsureAuraModelInput(modelInput, Params.Tracks[0].SigmaShift);
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
            patchedSamplerBaseModels[prop.Name] = baseModelInput;
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

        CreateAdditionalPatchedTrackOutputs(sharedClipPath, patchedSamplerBaseModels);

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

    private void CreateAdditionalPatchedTrackOutputs(JArray clipPath, Dictionary<string, JArray> patchedSamplerBaseModels)
    {
        if (Params.Tracks.Count <= 1
            || clipPath is null
            || patchedSamplerBaseModels.Count == 0)
        {
            return;
        }

        List<PatchedSamplerOutput> samplerOutputs = GetPatchedSamplerOutputs(patchedSamplerBaseModels);
        foreach (JsonParser.TrackSpec track in Params.Tracks.Skip(1))
        {
            foreach (PatchedSamplerOutput samplerOutput in samplerOutputs)
            {
                JArray trackClipPath = CreateTrackClipPath(track, samplerOutput.BranchIndex) ?? clipPath;
                JArray modelPathForSampling = CreateTrackSamplerModelPath(samplerOutput.ModelPath, track);
                _ = CreateTrackOutputBranch(
                    trackClipPath,
                    modelPathForSampling,
                    samplerOutput.VaePath,
                    track,
                    samplerOutput.BranchIndex
                );
            }
        }
    }

    private List<PatchedSamplerOutput> GetPatchedSamplerOutputs(Dictionary<string, JArray> patchedSamplerBaseModels)
    {
        List<PatchedSamplerOutput> outputs = [];
        int branchIndex = 0;

        foreach (JProperty prop in g.Workflow.Properties())
        {
            if (prop.Value is not JObject node
                || $"{node["class_type"]}" != NodeTypes.VAEDecodeAudio
                || node["inputs"] is not JObject decodeInputs
                || !TryGetInputSourceId(decodeInputs, "samples", out string samplerId)
                || !patchedSamplerBaseModels.TryGetValue(samplerId, out JArray modelPath)
                || !TryGetInputPath(decodeInputs, "vae", out JArray vaePath))
            {
                continue;
            }

            if (modelPath.Count < 2)
            {
                continue;
            }

            outputs.Add(new(
                BranchIndex: branchIndex,
                ModelPath: new JArray(modelPath),
                VaePath: new JArray(vaePath)
            ));
            branchIndex++;
        }

        return outputs;
    }

    private JArray CreateTrackClipPath(JsonParser.TrackSpec track, int branchIndex)
    {
        if (Helpers is null)
        {
            return null;
        }

        (string clip1Name, string clip1Url, string clip1Hash) = GetClipInfo(DefaultClip1);
        (string clip2Name, string clip2Url, string clip2Hash) = GetClipInfo(track.LmModel);
        string clip1 = Helpers.RequireClipModel(clip1Name, clip1Url, clip1Hash, null);
        string clip2 = Helpers.RequireClipModel(clip2Name, clip2Url, clip2Hash, null);
        string cacheKey = $"{clip1}|{clip2}";
        ClipPathCache ??= [];
        if (ClipPathCache.TryGetValue(cacheKey, out JArray cachedPath))
        {
            return new JArray(cachedPath);
        }

        string clipNode = g.CreateNode(NodeTypes.DualClipLoader, new JObject
        {
            ["clip_name1"] = clip1,
            ["clip_name2"] = clip2,
            ["type"] = "ace",
            ["device"] = "default"
        }, GetTrackNodeId(10, track.Index, branchIndex));
        JArray clipPath = new(clipNode, 0);
        ClipPathCache[cacheKey] = clipPath;
        return new JArray(clipPath);
    }

    private JArray CreateTrackOutputBranch(JArray clipPath, JArray modelPath, JArray vaePath, JsonParser.TrackSpec track, int branchIndex)
    {
        JArray positiveConditioning = CreateTrackConditioning(
            clipPath,
            track,
            GetTrackNodeId(30, track.Index, branchIndex)
        );
        JArray negativeConditioning = CreateTrackConditioning(
            clipPath,
            track,
            GetTrackNodeId(35, track.Index, branchIndex)
        );

        string latentNode = g.CreateNode(NodeTypes.EmptyAceStepAudioLatent, new JObject
        {
            ["batch_size"] = g.UserInput.Get(T2IParamTypes.BatchSize, 1),
            ["seconds"] = track.Duration
        }, GetTrackNodeId(40, track.Index, branchIndex));

        string zeroedNegativeNode = g.CreateNode(NodeTypes.ConditioningZeroOut, new JObject
        {
            ["conditioning"] = negativeConditioning
        }, GetTrackNodeId(45, track.Index, branchIndex));

        string samplerNode = g.CreateNode(NodeTypes.SwarmKSampler, new JObject
        {
            ["model"] = modelPath,
            ["noise_seed"] = Params.Seed,
            ["steps"] = track.Steps,
            ["cfg"] = track.AudioCfg,
            ["sampler_name"] = track.AudioSamplerName,
            ["scheduler"] = track.AudioScheduler,
            ["positive"] = positiveConditioning,
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
        }, GetTrackNodeId(50, track.Index, branchIndex));

        string decodedNode = g.CreateNode(NodeTypes.VAEDecodeAudio, new JObject
        {
            ["samples"] = new JArray(samplerNode, 0),
            ["vae"] = vaePath
        }, GetTrackNodeId(60, track.Index, branchIndex));

        JArray audioPath = new(decodedNode, 0);
        CreateTrackSaveNode(audioPath, track, branchIndex);
        return audioPath;
    }

    private void CreateTrackSaveNode(JArray audioPath, JsonParser.TrackSpec track, int branchIndex)
    {
        string branchSuffix = branchIndex > 0 ? $"_{branchIndex + 1}" : "";
        _ = g.CreateNode("SaveAudioMP3", new JObject
        {
            ["audio"] = audioPath,
            ["filename_prefix"] = $"SwarmUI_track_{track.Index + 1}{branchSuffix}_",
            ["quality"] = "V0"
        }, GetTrackNodeId(70, track.Index, branchIndex));
    }

    private JArray CreateTrackConditioning(JArray clipPath, JsonParser.TrackSpec track, string nodeId)
    {
        JObject promptInputs = new()
        {
            ["clip"] = clipPath
        };
        ApplyTrackInputs(promptInputs, track);
        string promptNode = g.CreateNode(NodeTypes.TextEncodeAceStepAudio, promptInputs, nodeId);
        return new JArray(promptNode, 0);
    }

    private string GetTrackNodeId(int baseOffset, int trackIndex, int branchIndex)
    {
        return g.GetStableDynamicID(AudioIdBase + (branchIndex * 1000) + (trackIndex * 100) + baseOffset, 0);
    }

    private void ApplyTrackInputs(JObject encodeInputs, JsonParser.TrackSpec track)
    {
        encodeInputs["lyrics"] = track.Prompt;
        encodeInputs["tags"] = track.Style;
        encodeInputs["seed"] = Params.ConditioningSeed + track.Index;
        encodeInputs["bpm"] = track.Bpm;
        encodeInputs["duration"] = track.Duration;
        encodeInputs["timesignature"] = track.TimeSignature;
        encodeInputs["language"] = track.Language;
        encodeInputs["keyscale"] = track.KeyScale;
        encodeInputs["generate_audio_codes"] = true;
        encodeInputs["cfg_scale"] = track.LmCfgScale;
        encodeInputs["temperature"] = 0.85;
        encodeInputs["top_p"] = 0.9;
        encodeInputs["top_k"] = 0;
        encodeInputs["min_p"] = 0;
    }

    private JArray ApplyConfiguredLorasToSamplerModel(JArray modelPath)
    {
        return ApplyConfiguredLorasToSamplerModel(modelPath, 0);
    }

    private JArray ApplyConfiguredLorasToSamplerModel(JArray modelPath, int trackIndex)
    {
        if (TryGetAuraInputs(modelPath, out JObject auraInputs))
        {
            JArray innerModelPath = GetInputPath(auraInputs, "model");
            auraInputs["model"] = ApplyConfiguredLoras(innerModelPath, trackIndex);
            return modelPath;
        }
        return ApplyConfiguredLoras(modelPath, trackIndex);
    }

    private JArray CreateTrackSamplerModelPath(JArray baseModelPath, JsonParser.TrackSpec track)
    {
        JArray modelPath = new(baseModelPath);
        if (TryGetAuraInputs(modelPath, out JObject auraInputs))
        {
            modelPath = GetInputPath(auraInputs, "model");
        }
        modelPath = ApplyConfiguredLoras(modelPath, track.Index);
        return EnsureAuraModelInput(modelPath, track.SigmaShift);
    }

    private JArray ApplyConfiguredLoras(JArray modelPath)
    {
        return ApplyConfiguredLoras(modelPath, 0);
    }

    private JArray ApplyConfiguredLoras(JArray modelPath, int trackIndex)
    {
        if (modelPath.Count < 2)
        {
            return modelPath;
        }

        ExistingLoraChain existingChain = ExtractExistingLoraChain(modelPath);
        List<AceStepLora> configuredLoras = AceStepLoraParser.ResolveRelevantLoras(g.UserInput, g.ModelFolderFormat, trackIndex);
        List<AceStepLora> combinedLoras = [];
        HashSet<string> seenLoras = [];

        foreach (AceStepLora lora in existingChain.Loras)
        {
            AddLoraIfNew(combinedLoras, seenLoras, lora);
        }
        foreach (AceStepLora lora in configuredLoras)
        {
            AddLoraIfNew(combinedLoras, seenLoras, lora);
        }

        if (!existingChain.RequiresNormalization
            && existingChain.Loras.Count == combinedLoras.Count
            && LoraListsMatch(existingChain.Loras, combinedLoras))
        {
            return modelPath;
        }

        JArray patchedModelPath = existingChain.RootModelPath;
        foreach (AceStepLora lora in combinedLoras)
        {
            string loraNode = g.CreateNode(NodeTypes.LoraLoaderModelOnly, new JObject
            {
                ["model"] = patchedModelPath,
                ["lora_name"] = lora.ModelName,
                ["strength_model"] = lora.Weight
            });
            patchedModelPath = new JArray(loraNode, 0);
        }

        return patchedModelPath;
    }

    private ExistingLoraChain ExtractExistingLoraChain(JArray modelPath)
    {
        List<AceStepLora> loras = [];
        bool requiresNormalization = false;
        JArray currentPath = modelPath;

        while (TryGetSourceNode(currentPath, out JObject sourceNode)
            && sourceNode["inputs"] is JObject sourceInputs)
        {
            string classType = $"{sourceNode["class_type"]}";
            if (classType != NodeTypes.LoraLoader && classType != NodeTypes.LoraLoaderModelOnly)
            {
                break;
            }

            requiresNormalization = requiresNormalization || classType == NodeTypes.LoraLoader;
            if (!TryReadExistingLora(sourceInputs, out AceStepLora lora))
            {
                break;
            }
            if (!TryGetInputPath(sourceInputs, "model", out JArray nextPath))
            {
                break;
            }

            loras.Add(lora);
            currentPath = nextPath;
        }

        loras.Reverse();
        return new ExistingLoraChain(currentPath, loras, requiresNormalization);
    }

    private static bool TryReadExistingLora(JObject sourceInputs, out AceStepLora lora)
    {
        lora = null;
        if (!sourceInputs.TryGetValue("lora_name", out JToken loraNameTok))
        {
            return false;
        }

        double weight = 1;
        if (sourceInputs.TryGetValue("strength_model", out JToken strengthTok))
        {
            weight = strengthTok.Value<double>();
        }

        string loraName = $"{loraNameTok}";
        if (string.IsNullOrWhiteSpace(loraName))
        {
            return false;
        }

        lora = new AceStepLora(loraName, weight);
        return true;
    }

    private static void AddLoraIfNew(List<AceStepLora> loras, HashSet<string> seenLoras, AceStepLora lora)
    {
        string signature = GetLoraSignature(lora);
        if (seenLoras.Add(signature))
        {
            loras.Add(lora);
        }
    }

    private static string GetLoraSignature(AceStepLora lora)
    {
        return $"{lora.ModelName}|{lora.Weight}";
    }

    private static bool LoraListsMatch(List<AceStepLora> first, List<AceStepLora> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            if (GetLoraSignature(first[i]) != GetLoraSignature(second[i]))
            {
                return false;
            }
        }

        return true;
    }

    private JArray NormalizeClipPath(JArray clipPath)
    {
        JArray currentPath = clipPath;
        while (TryGetSourceNode(currentPath, out JObject sourceNode)
            && $"{sourceNode["class_type"]}" == NodeTypes.LoraLoader
            && sourceNode["inputs"] is JObject sourceInputs
            && TryGetInputPath(sourceInputs, "clip", out JArray nextPath))
        {
            currentPath = nextPath;
        }

        return currentPath;
    }

    private JArray EnsureAuraModelInput(JArray modelPath)
    {
        return EnsureAuraModelInput(modelPath, Params.SigmaShift);
    }

    private JArray EnsureAuraModelInput(JArray modelPath, double sigmaShift)
    {
        if (modelPath.Count < 2)
        {
            return [];
        }

        if (TryGetAuraInputs(modelPath, out JObject auraInputs))
        {
            auraInputs["shift"] = sigmaShift;
            return modelPath;
        }

        string auraNode = g.CreateNode(NodeTypes.ModelSamplingAuraFlow, new JObject
        {
            ["model"] = modelPath,
            ["shift"] = sigmaShift
        });
        return new JArray(auraNode, 0);
    }

    private bool TryGetAuraInputs(JArray modelPath, out JObject auraInputs)
    {
        auraInputs = null;
        if (!TryGetSourceNode(modelPath, out JObject sourceNode)
            || $"{sourceNode["class_type"]}" != NodeTypes.ModelSamplingAuraFlow
            || sourceNode["inputs"] is not JObject sourceInputs)
        {
            return false;
        }

        auraInputs = sourceInputs;
        return true;
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

    private List<JsonParser.TrackSpec> GetTracks()
    {
        return new JsonParser(g).ParseTracks();
    }

    private string ResolvePrompt()
    {
        return ResolvePrompt(0);
    }

    private string ResolvePrompt(int trackIndex)
    {
        return PromptParser.ResolvePrompt(g.UserInput, trackIndex);
    }

    private static bool IsInputFromNodeSet(JObject inputs, string inputName, HashSet<string> nodeIds)
    {
        return TryGetInputSourceId(inputs, inputName, out string sourceNodeId) && nodeIds.Contains(sourceNodeId);
    }

    private static JArray GetInputPath(JObject inputs, string inputName)
    {
        return TryGetInputPath(inputs, inputName, out JArray path) ? path : [];
    }

    private static bool TryGetInputPath(JObject inputs, string inputName, out JArray path)
    {
        path = null;
        if (!inputs.TryGetValue(inputName, out JToken inputTok) || inputTok is not JArray rawPath || rawPath.Count < 1)
        {
            return false;
        }
        path = rawPath;
        return true;
    }

    private bool TryGetSourceNode(JArray path, out JObject sourceNode)
    {
        sourceNode = null;
        if (path.Count < 1)
        {
            return false;
        }
        string sourceNodeId = $"{path[0]}";
        return g.Workflow.TryGetValue(sourceNodeId, out JToken sourceTok)
            && (sourceNode = sourceTok as JObject) is not null;
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
