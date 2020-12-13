using System;
using System.Linq;
using System.Text;

namespace Generators
{
    public static class Extensions
    {
        public static StringBuilder Indent(this StringBuilder sb, string input, int indentLevel = 0, int indentSize = 4, 
            bool newLineOnLast = true, bool skipFirst = true)
        {
            var splitted = input.Split(new []{ Environment.NewLine }, StringSplitOptions.None).ToList();
            var indent = string.Empty.PadLeft(indentLevel * indentSize);
            foreach (var line in (skipFirst ? splitted.Skip(1) : splitted))
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