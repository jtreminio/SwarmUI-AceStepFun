using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;

namespace AceStepFun;

internal sealed record AceStepLora(string ModelName, double Weight);

internal static class AceStepLoraParser
{
    public static bool HasRelevantLoras(T2IParamInput input)
    {
        if (!input.TryGet(T2IParamTypes.Loras, out List<string> loras) || loras.Count == 0)
        {
            return false;
        }

        List<string> confinements = input.Get(T2IParamTypes.LoraSectionConfinement);
        for (int i = 0; i < loras.Count; i++)
        {
            if (IsAceStepConfinement(GetConfinement(confinements, i)))
            {
                return true;
            }
        }

        return false;
    }

    public static List<AceStepLora> ResolveRelevantLoras(T2IParamInput input, string modelFolderFormat)
    {
        return ResolveRelevantLoras(input, modelFolderFormat, 0);
    }

    public static List<AceStepLora> ResolveRelevantLoras(T2IParamInput input, string modelFolderFormat, int trackIndex)
    {
        if (!input.TryGet(T2IParamTypes.Loras, out List<string> loras) || loras.Count == 0)
        {
            return [];
        }
        if (!Program.T2IModelSets.TryGetValue("LoRA", out T2IModelHandler loraHandler))
        {
            return [];
        }

        List<string> weights = input.Get(T2IParamTypes.LoraWeights);
        List<string> confinements = input.Get(T2IParamTypes.LoraSectionConfinement);
        if (confinements is not null && confinements.Count > loras.Count)
        {
            confinements = null;
        }

        List<AceStepLora> result = [];
        for (int i = 0; i < loras.Count; i++)
        {
            if (!IsRelevantConfinement(GetConfinement(confinements, i), trackIndex))
            {
                continue;
            }

            T2IModel lora = ResolveLora(loraHandler, loras[i]);
            double weight = weights is null || i >= weights.Count ? 1 : double.Parse(weights[i]);
            result.Add(new AceStepLora(lora.ToString(modelFolderFormat), weight));
        }

        return result;
    }

    private static T2IModel ResolveLora(T2IModelHandler handler, string name)
    {
        if (!handler.Models.TryGetValue(name + ".safetensors", out T2IModel lora)
            && !handler.Models.TryGetValue(name, out lora))
        {
            throw new SwarmUserErrorException($"LoRA Model '{name}' not found in the model set.");
        }

        return lora;
    }

    private static int GetConfinement(List<string> confinements, int index)
    {
        if (confinements is null || confinements.Count <= index)
        {
            return -1;
        }

        return int.Parse(confinements[index]);
    }

    private static bool IsRelevantConfinement(int confinement, int trackIndex)
    {
        return confinement == -1
            || confinement == 0
            || confinement == T2IParamInput.SectionID_BaseOnly
            || confinement == AceStepFunExtension.SectionID_Audio
            || confinement == AceStepFunExtension.AceStepSectionIdForTrack(trackIndex);
    }

    private static bool IsAceStepConfinement(int confinement)
    {
        return confinement == -1
            || confinement == 0
            || confinement == T2IParamInput.SectionID_BaseOnly
            || confinement >= AceStepFunExtension.SectionID_Audio;
    }
}
