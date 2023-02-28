using BrokenDependency;
using Verse;

// An assembly which is missing a dependency at runtime
// Used for testing, Prepatcher should handle such cases gracefully

namespace BrokenAssembly
{
    public static class Class1
    {
        public static Pawn Test1()
        {
            return null;
        }

        public static MissingClass Test(MissingEnum missing = MissingEnum.Missing)
        {
            return null;
        }
    }
}
