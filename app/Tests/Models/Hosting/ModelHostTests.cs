using AIStudio.Models;
using AIStudio.Models.Hosting;
using AIStudio.Models.Matching;
using AIStudio.Models.Registry;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Hosting;

/// <summary>
/// Checks the hosts the app actually ships, against the names the providers actually answer with.
/// </summary>
/// <remarks>
/// The names in here are the ones from the corpus, which came out of the provider lists and the
/// audit rather than out of somebody's head. What is being asked is the routing question only --
/// what is left of a name once the way it arrived has been accounted for, and which APIs survive
/// the trip. Which model it then is remains a question for the rules.
/// </remarks>
[TestFixture]
public sealed class ModelHostTests
{
    /// <summary>
    /// The hosts as the app has them, found by the generator rather than listed here.
    /// </summary>
    private static readonly ModelHostIndex INDEX = ModelHostIndex.Build(ModelRegistrations.CreateHosts());

    [Test]
    public void EveryProviderAPersonCanConfigureHasAHost()
    {
        //
        // This is the one which fails when somebody adds a provider to the app and stops there. It
        // is not a runtime error -- names would simply be taken as they arrive -- so nothing else
        // would ever point it out.
        //
        Assert.That(INDEX.ProvidersWithoutAHost, Is.Empty);
    }

    [Test]
    public void EveryHostSaysWhereItsBehaviourCanBeCheckedAndWhen()
    {
        var unstated = INDEX.Hosts.Where(host => !host.Source.IsStated).Select(host => host.GetType().Name);

        Assert.That(unstated, Is.Empty);
    }

    [Test]
    public void AGatewayNameFallsApartIntoTheModelAndWhoBuiltIt()
    {
        var unwrapped = Unwrap(LLMProviders.OPEN_ROUTER, "anthropic/claude-opus-5", out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(unwrapped.Original, Is.EqualTo("claude-opus-5"));
            Assert.That(vendor, Is.EqualTo(ModelVendor.ANTHROPIC));
        });
    }

    [Test]
    public void TheHuggingFaceRouterTakesOffTheRouteFirstAndTheOrganizationSecond()
    {
        var unwrapped = Unwrap(LLMProviders.HUGGINGFACE, "openai/gpt-oss-120b:fireworks-ai", out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(unwrapped.Original, Is.EqualTo("gpt-oss-120b"));
            Assert.That(vendor, Is.EqualTo(ModelVendor.OPEN_AI), "OpenAI published the weights, whoever is serving them today.");
        });
    }

    [Test]
    public void AHuggingFaceNameWithoutARouteIsStillTakenApart()
    {
        var unwrapped = Unwrap(LLMProviders.HUGGINGFACE, "deepseek-ai/DeepSeek-R1-Distill-Qwen-32B", out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(unwrapped.Original, Is.EqualTo("DeepSeek-R1-Distill-Qwen-32B"));
            Assert.That(vendor, Is.EqualTo(ModelVendor.DEEP_SEEK));
        });
    }

    [Test]
    public void TheFireworksAccountPathComesOffWholeWithoutAnybodyCountingItsSegments()
    {
        var unwrapped = Unwrap(LLMProviders.FIREWORKS, "accounts/fireworks/models/llama-v3p1-405b-instruct", out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(unwrapped.Original, Is.EqualTo("llama-v3p1-405b-instruct"));
            Assert.That(vendor, Is.Null, "None of the three path segments names a vendor.");
        });
    }

    [Test]
    public void BlabladorLosesItsPlaceInTheMenu()
    {
        var unwrapped = Unwrap(LLMProviders.HELMHOLTZ, "1 - Llama3 405 the best general model", out _);

        Assert.That(unwrapped.Original, Is.EqualTo("Llama3 405 the best general model"));
    }

    [Test]
    public void AnEngineServingAHubRepositoryHasItReadAsOne()
    {
        var unwrapped = Unwrap(LLMProviders.SELF_HOSTED, "meta-llama/Llama-3.3-70B-Instruct", out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(unwrapped.Original, Is.EqualTo("Llama-3.3-70B-Instruct"));
            Assert.That(vendor, Is.EqualTo(ModelVendor.META));
        });
    }

    [Test]
    public void TheVariantOllamaWritesAfterAColonSurvives()
    {
        //
        // The colon means two different things at two different hosts. On the router it says where
        // the request goes; on Ollama it says which build is running, and taking it off would leave
        // a name which no longer identifies the model.
        //
        var unwrapped = Unwrap(LLMProviders.SELF_HOSTED, "qwen3.8:27b-mlx", out _);

        Assert.That(unwrapped.Original, Is.EqualTo("qwen3.8:27b-mlx"));
    }

    [Test]
    public void AResellerLeavesTheNameAloneAndOnlyTakesTheApiAway()
    {
        //
        // This is the GWDG case: it offers Claude and GPT under the names their vendors use, so the
        // rules recognize them and answer with everything those models can do. Everything except
        // the API -- the request goes to Göttingen, and the Responses API is not served there.
        //
        var atItsVendor = new ModelProfile { Capabilities = Capability.TEXT_INPUT | Capability.FUNCTION_CALLING | Capability.RESPONSES_API };
        var throughTheReseller = Transport(LLMProviders.GWDG, atItsVendor);

        Assert.Multiple(() =>
        {
            Assert.That(Unwrap(LLMProviders.GWDG, "claude-sonnet-5", out _).Original, Is.EqualTo("claude-sonnet-5"));
            Assert.That(throughTheReseller.Has(Capability.FUNCTION_CALLING), Is.True);
            Assert.That(throughTheReseller.Has(Capability.RESPONSES_API), Is.False);
            Assert.That(throughTheReseller.Has(Capability.CHAT_COMPLETION_API), Is.True);
        });
    }

    [Test]
    public void OnlyOpenAIsOwnCloudKeepsTheResponsesApi()
    {
        var withBothApis = new ModelProfile { Capabilities = Capability.RESPONSES_API | Capability.CHAT_COMPLETION_API };
        var elsewhere = INDEX.Hosts
            .Where(host => host.Provider is not LLMProviders.OPEN_AI)
            .Where(host => host.ApplyTransport(withBothApis).Has(Capability.RESPONSES_API))
            .Select(host => host.GetType().Name);

        Assert.Multiple(() =>
        {
            Assert.That(Transport(LLMProviders.OPEN_AI, withBothApis).Has(Capability.RESPONSES_API), Is.True);
            Assert.That(elsewhere, Is.Empty, "The app sends a Responses API request from exactly one place.");
        });
    }

    [Test]
    public void AModelReachedThroughNeitherApiIsNotGivenOne()
    {
        //
        // An embedding model is reached through neither of the two. Answering that it speaks the
        // chat completion API would be a claim nobody made.
        //
        var embedding = new ModelProfile { Capabilities = Capability.EMBEDDING };
        var throughAGateway = Transport(LLMProviders.OPEN_ROUTER, embedding);

        Assert.Multiple(() =>
        {
            Assert.That(throughAGateway.Has(Capability.EMBEDDING), Is.True);
            Assert.That(throughAGateway.HasAny(Capability.CHAT_COMPLETION_API | Capability.RESPONSES_API), Is.False);
        });
    }

    [Test]
    public void ANameWithoutAWrappingComesBackAsItWas()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Unwrap(LLMProviders.OPEN_AI, "gpt-5.6", out _).Original, Is.EqualTo("gpt-5.6"));
            Assert.That(Unwrap(LLMProviders.GROQ, "llama-3.3-70b-versatile", out _).Original, Is.EqualTo("llama-3.3-70b-versatile"));
            Assert.That(Unwrap(LLMProviders.LITE_LLM, "the-fast-one", out _).Original, Is.EqualTo("the-fast-one"));
        });
    }

    private static ModelId Unwrap(LLMProviders provider, string modelId, out ModelVendor? declaredVendor) => INDEX.Unwrap(new ModelId(modelId), provider, out declaredVendor);

    private static ModelProfile Transport(LLMProviders provider, in ModelProfile profile) => INDEX.ApplyTransport(profile, provider);
}