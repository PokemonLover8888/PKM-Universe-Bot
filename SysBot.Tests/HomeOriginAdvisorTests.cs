using FluentAssertions;
using PKHeX.Core;
using SysBot.Pokemon;
using SysBot.Pokemon.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace SysBot.Tests;

public class HomeOriginAdvisorTests
{
    private readonly ITestOutputHelper _out;
    public HomeOriginAdvisorTests(ITestOutputHelper o) => _out = o;
    static HomeOriginAdvisorTests() => AutoLegalityWrapper.EnsureInitialized(new Pokemon.LegalitySettings());

    [Fact]
    public void EternatusFromAnSvBot_IsDeclinedAndPointsAtSwSh()
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var pk = sav.GetLegalForTrade(AutoLegalityWrapper.GetTemplate(new ShowdownSet("Eternatus\nShiny: Yes")), out _);

        HomeOriginAdvisor.IsNativeToBot(pk).Should().BeFalse("Eternatus is a Sword/Shield event, not an SV native");

        var msg = HomeOriginAdvisor.BuildDeclineMessage(pk, "Eternatus", "Scarlet/Violet");
        _out.WriteLine(msg);
        msg.Should().Contain("Sword/Shield");
        msg.Should().Contain("Celebi-SWSH");
    }

    [Fact]
    public void EternatusFromASwShBot_IsFine()
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        var pk = sav.GetLegalForTrade(AutoLegalityWrapper.GetTemplate(new ShowdownSet("Eternatus\nShiny: Yes")), out _);

        HomeOriginAdvisor.IsNativeToBot(pk).Should().BeTrue("a SwSh bot CAN make a HOME-ready Eternatus");
        pk.IsShiny.Should().BeTrue();
    }

    [Fact]
    public void GoOnlyPokemon_SendsThemToTheArchive()
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var pk = sav.GetLegalForTrade(AutoLegalityWrapper.GetTemplate(new ShowdownSet("Diancie\nShiny: Yes")), out _);

        HomeOriginAdvisor.IsNativeToBot(pk).Should().BeFalse();
        var msg = HomeOriginAdvisor.BuildDeclineMessage(pk, "Diancie", "Scarlet/Violet");
        _out.WriteLine(msg);
        msg.Should().Contain("Archives");
    }

    [Theory]
    [InlineData("Garchomp")]
    [InlineData("Gengar")]
    public void OrdinarySvPokemon_IsNativeAndNotDeclined(string species)
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        var pk = sav.GetLegalForTrade(AutoLegalityWrapper.GetTemplate(new ShowdownSet($"{species}\nShiny: Yes")), out _);
        HomeOriginAdvisor.IsNativeToBot(pk).Should().BeTrue($"{species} is obtainable in SV — must NOT be declined");
    }

    // A competitive Conkeldurr with Knock Off (no SwSh TM/TR) is legal ONLY as a Gen 7 transfer, so the
    // full set builds Non-Native — but the SPECIES is SwSh-native (Isle of Armor). The decline gate must
    // ship it (native-species probe succeeds) instead of redirecting. Guards the "$t Conkeldurr comp set
    // wrongly declined as 'originates in UM'" fix in Helpers.ProcessShowdownSetAsync.
    [Fact]
    public void NativeSpecies_NonNativeSet_ShipsInsteadOfDeclining()
    {
        var sav = AutoLegalityWrapper.GetTrainerInfo<PK8>();
        const string compSet =
            "Conkeldurr (M) @ Flame Orb\nAbility: Guts\nEVs: 252 HP / 252 Atk / 4 SpD\nAdamant Nature\n" +
            "- Drain Punch\n- Mach Punch\n- Knock Off\n- Facade";

        // The full requested set is legal but Non-Native (Gen 7 origin) — this is what USED to be declined.
        var full = sav.GetLegalForTrade(AutoLegalityWrapper.GetTemplate(new ShowdownSet(compSet)), out _);
        new LegalityAnalysis(full).Valid.Should().BeTrue("the requested Conkeldurr set is legal as a transfer");
        HomeOriginAdvisor.IsNativeToBot(full).Should().BeFalse("Knock Off forces a Gen 7 origin, so it's Non-Native");

        // The gate signal: a bare-species native probe must SUCCEED for SwSh Conkeldurr → ship, not decline.
        var probe = sav.GetLegalNativeDirect(AutoLegalityWrapper.GetTemplate(new ShowdownSet("Conkeldurr")));
        probe.Should().NotBeNull("Conkeldurr IS native to SwSh (Isle of Armor) — the set, not the species, is the issue");
        HomeOriginAdvisor.IsNativeToBot(probe!).Should().BeTrue();

        // Contrast: a genuinely foreign species probes null and MUST still redirect (gate stays closed).
        var svSav = AutoLegalityWrapper.GetTrainerInfo<PK9>();
        svSav.GetLegalNativeDirect(AutoLegalityWrapper.GetTemplate(new ShowdownSet("Eternatus")))
            .Should().BeNull("Eternatus has no SV-native encounter — must still redirect to a SwSh bot");
    }
}
