using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Verse;

namespace Prepatcher
{
    public static class Allocs
    {
        private static Dictionary<string, int> allocs = new();

        public static void Allocd(string type)
        {
            if (UnityData.IsInMainThread)
                allocs[type] = allocs.GetValueOrDefault(type) + 1;
        }

        public static void Update()
        {
            if (Input.GetKey(KeyCode.End))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    allocs.Clear();
                }
                else
                {
                    var str = new StringBuilder();
                    foreach (var (ctor, count) in allocs.OrderByDescending(kv => kv.Value))
                    {
                        str.AppendLine($"{ctor} {count}");
                    }
                    File.WriteAllText("closures.txt", str.ToString());
                }
            }
        }
    }
}
