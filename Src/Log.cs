
using ToyCompiler.Data;
using System.Text;

namespace ToyCompiler;

/// <summary>
/// 输出信息
/// </summary>
public class Log
{
	private static readonly StringBuilder sb = new();
	private static int Line = 0;
	public static void Print(string st)
	{
		sb.Append(st);
		sb.Append("\r\n");
	}
	public static void Print(InsID id, string str1, string str2)
	{
		sb.Append("\t" + Line + "\t" + id.ToString() + '\t' + str1 + '\t' + str2);
		sb.Append("\r\n");
		Line++;
	}
	public static void Print(InsID id, string str1, string str2, string str3)
	{
		sb.Append("\t" + Line + "\t" + id.ToString() + '\t' + str1 + '\t' + str2 + '\t' + str3);
		sb.Append("\r\n");
		Line++;
	}
	public static void Out()
	{
		Console.WriteLine(sb.ToString());
		sb.Clear();
	}
}