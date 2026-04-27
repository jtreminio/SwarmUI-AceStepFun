using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace AceStepFun;

public class Runner(WorkflowGenerator g)
{
    public void Run()
    {
        if (!IsExtensionActive())
        {
            return;
        }

        new AudioWorkflow(g).Run();
    }

    private bool IsExtensionActive()
    {
        T2IParamType modelType = AceStepFunExtension.Model?.Type;
        if (modelType is not null && g.UserInput.TryGetRaw(modelType, out _))
        {
            return true;
        }

        return IsMainModelAceStep() && HasAnyAce2VideoOverride();
    }

    private bool IsMainModelAceStep()
    {
        return g.UserInput.TryGet(T2IParamTypes.Model, out T2IModel model)
            && model?.ModelClass?.CompatClass == T2IModelClassSorter.CompatAceStep15;
    }

    private bool HasAnyAce2VideoOverride()
    {
        return HasRaw(AceStepFunExtension.AudioCfg)
            || HasRaw(AceStepFunExtension.LmCfg)
            || HasRaw(AceStepFunExtension.Steps)
            || HasRaw(AceStepFunExtension.LmModel)
            || HasRaw(AceStepFunExtension.Prompt)
            || HasRaw(AceStepFunExtension.Style)
            || HasRaw(AceStepFunExtension.Duration)
            || HasRaw(AceStepFunExtension.Bpm)
            || HasRaw(AceStepFunExtension.TimeSignature)
            || HasRaw(AceStepFunExtension.Language)
            || HasRaw(AceStepFunExtension.KeyScale)
            || HasRaw(AceStepFunExtension.MusicTracks)
            || HasRaw(T2IParamTypes.Text2AudioStyle)
            || HasRaw(T2IParamTypes.Text2AudioDuration)
            || HasRaw(T2IParamTypes.Text2AudioBPM)
            || HasRaw(T2IParamTypes.Text2AudioTimeSignature)
            || HasRaw(T2IParamTypes.Text2AudioLanguage)
            || HasRaw(T2IParamTypes.Text2AudioKeyScale)
            || HasRaw(AceStepFunExtension.Text2AudioPrompt)
            || HasRaw(AceStepFunExtension.Text2AudioLmModel)
            || HasRaw(AceStepFunExtension.Text2AudioAudioCfg)
            || HasRaw(AceStepFunExtension.Text2AudioLmCfg)
            || HasRaw(AceStepFunExtension.Text2AudioSteps)
            || HasRaw(AceStepFunExtension.Text2AudioSigmaShift)
            || AceStepLoraParser.HasRelevantLoras(g.UserInput)
            || PromptParser.HasAudioSection(g.UserInput.Get(T2IParamTypes.Prompt, ""));
    }

    private bool HasRaw<T>(T2IRegisteredParam<T> param)
    {
        return param?.Type is not null && g.UserInput.TryGetRaw(param.Type, out _);
    }
}
