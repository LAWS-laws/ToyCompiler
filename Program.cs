using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace CompilerToy;

public static class Proj
{
	public static void Main(string[] args)
	{
		string code;
		try
		{
			
			code = File.ReadAllText("Source.java",Encoding.Default);
		}
		catch (Exception ex)
		{
			Console.Write("读取源文件时发生了错误："+ex.Message);
			Console.ReadKey();
			return;
		}
		Compiler comp = new Compiler();
		comp.PreProcess(code);		//预处理
		comp.Compile();				//编译
		Program prog = comp.Link();	//链接
		Console.WriteLine("=====开始运行=====");
		DateTime dt = DateTime.Now;
		VirtualMachine vm = new VirtualMachine(prog);
		vm.Run();
		Console.WriteLine("=====运行结束=====耗时："+(DateTime.Now-dt).TotalMilliseconds + "毫秒");
		Console.ReadKey();
	}
	private unsafe static void JJJ()
	{
		for (int i = 0; i < 255; i++)
		{
			char cc = (char)i;
			Console.Write("Char: " + cc);
			if (char.IsPunctuation(cc)) Console.Write(" Punc ");
			if (char.IsSymbol(cc)) Console.Write(" Synbol ");
			if (char.IsSeparator(cc)) Console.Write(" Sep ");
			if (char.IsSurrogate(cc)) Console.Write(" Sur ");
			Console.WriteLine();
		}
	}
}