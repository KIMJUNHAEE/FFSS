using System;
using System.Collections.Generic;
using System.Linq;
using CardBattle;
using UnityEngine;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        PokerHandsSupportWildJokers();
        SignatureCatalogIsComplete();
        SignatureSeotdaCardsTriggerOnTheirOwnCombos();
        Console.WriteLine($"PASS: {assertions} assertions");
    }

    private static void PokerHandsSupportWildJokers()
    {
        var naturalRoyal = Evaluate("H-1", "H-10", "H-11", "H-12", "H-13");
        Assert(naturalRoyal.Rank == PokerHandRank.RoyalFlush, "natural royal flush");
        Assert(naturalRoyal.JokerCount == 0, "natural hand has no joker");

        var redJokerRoyal = Evaluate("X-R", "H-10", "H-11", "H-12", "H-13");
        Assert(redJokerRoyal.Rank == PokerHandRank.RoyalFlush, "red joker completes royal flush");
        Assert(redJokerRoyal.HasRedJoker && redJokerRoyal.JokerCount == 1, "red joker metadata");
        Assert(redJokerRoyal.SuitCounts[CardSuit.Heart] == 5, "joker resolves to winning suit");

        var blackJokerQuads = Evaluate("X-B", "S-7", "C-7", "H-7", "D-2");
        Assert(blackJokerQuads.Rank == PokerHandRank.FullHouse,
            "black joker cannot complete four of a kind and instead pairs the deuce");
        Assert(blackJokerQuads.HasBlackJoker, "black joker metadata");

        var doubleJokerStraightFlush = Evaluate("X-R", "X-B", "S-9", "S-10", "S-11");
        Assert(doubleJokerStraightFlush.Rank == PokerHandRank.Straight,
            "red and black jokers cannot both become spades to complete a straight flush");
        Assert(doubleJokerStraightFlush.JokerCount == 2, "two joker count");
    }

    private static void SignatureCatalogIsComplete()
    {
        Assert(OpponentSeotdaCardCatalog.All.Count == 17, "17 signature cards");
        Assert(OpponentSeotdaCardCatalog.All.Select(card => card.BossId).Distinct().Count() == 17,
            "signature boss IDs are unique");
        Assert(OpponentSeotdaCardCatalog.All.Select(card => card.CardId).Distinct().Count() == 17,
            "signature card IDs are unique");
    }

    private static void SignatureSeotdaCardsTriggerOnTheirOwnCombos()
    {
        AssertSignature("38", "03_벚꽃_1", "38광땡");
        AssertSignature("구사", "09_국화_3", "3끗");
        AssertSignature("땡잡이", "07_홍싸리_3", "망통");
        AssertSignature("멍구사", "04_흑싸리_3", "3끗");
        AssertSignature("암행어사", "07_홍싸리_3", "1끗");
    }

    private static void AssertSignature(string bossId, string partnerName, string expectedBaseHand)
    {
        var definition = OpponentSeotdaCardCatalog.Find(bossId);
        var signatureSprite = new Sprite(definition.CardId);
        var result = SeotdaHandEvaluator.EvaluateDetails(signatureSprite, new Sprite(partnerName),
            definition, signatureSprite);
        Assert(result.HasSignatureCard, $"{bossId} signature recognized");
        Assert(result.SignatureTriggered, $"{bossId} signature trigger");
        Assert(result.DisplayName.Contains(expectedBaseHand), $"{bossId} base hand retained");
    }

    private static PokerHandResult Evaluate(params string[] names) =>
        PokerHandEvaluator.EvaluateDetails(names.Select(name => new Sprite(name)).ToList());

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
    }
}
