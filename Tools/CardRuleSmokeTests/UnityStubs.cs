namespace UnityEngine
{
    public class Sprite
    {
        public Sprite(string spriteName)
        {
            name = spriteName;
        }

        public string name { get; set; }
    }

    public static class Mathf
    {
        public static int Clamp(int value, int minimum, int maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : class => null;
    }
}

namespace CardBattle
{
    public enum CardSuit
    {
        None,
        Spade,
        Clover,
        Heart,
        Diamond,
    }
}
