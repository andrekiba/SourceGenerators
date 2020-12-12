using System;
using System.Linq;
using System.Text;

namespace Generators
{
    public static class Extensions
    {
        public static StringBuilder Indent(this StringBuilder sb, string input, int indentLevel = 0, int indentSize = 4, bool newLineOnLast = true)
        {
            var splitted = input.Split(new []{ Environment.NewLine }, StringSplitOptions.None);
            var indent = string.Empty.PadLeft(indentLevel * indentSize);
            foreach (var line in splitted)
            {
                sb.Append(indent);
                if (line == splitted.Last())
                {
                    if(newLineOnLast)
                        sb.AppendLine(line);
                    else
                        sb.Append(line);
                }
                else
                    sb.AppendLine(line);
            }
		
            return sb;
        }
    }
}