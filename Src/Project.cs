
using System.Text;

using ToyCompiler.Data;
using ToyCompiler.VM;

namespace ToyCompiler;

public static class Project
{
	public static void Main()
	{
		Console.WriteLine("ToyCompiler v1  made by Laws");
		Console.WriteLine("输入help查看可用命令");
		
		while (true)
		{
			Console.Write(">> ");
			string input = Console.ReadLine()??string.Empty;
			if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
			{
				break;
			}
			else if(input.Equals("help", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("help: 列出所有命令");
				Console.WriteLine("exit: 退出");
				Console.WriteLine("run + sourcefilepath（空一格）:编译并运行代码");
			}
			else if(input.StartsWith("run", StringComparison.OrdinalIgnoreCase))
			{
				string code;
				try
				{
					code = File.ReadAllText(input[4..], Encoding.Default);
				}
				catch (Exception ex)
				{
					Console.WriteLine("读取源文件时发生了错误：" + ex.Message);
					goto DONE;
				}
				CompiledProgram prog;
				try
				{
					Console.WriteLine();
					CompilingProcess comp = new();
					comp.PreProcess(code);      //预处理
					comp.Compile();             //编译
					prog = comp.Link(); //链接
				}
				catch(Exception ex)
				{
					Console.WriteLine("编译错误: " + ex.Message);
					goto DONE;
				}
				try
				{
					Console.WriteLine();
					Console.WriteLine(">> 开始运行");
					DateTime dt = DateTime.Now;
					VirtualMachine vm = new(prog);
					vm.Run();
					Console.WriteLine(">> 运行耗时：" + (DateTime.Now - dt).TotalMilliseconds + "milsec");
					Console.ReadKey();
				}
				catch (Exception ex)
				{
					Console.WriteLine("运行错误: " + ex.Message);
					goto DONE;
				}
			DONE:;
				GC.Collect();
			}
		}

		return;
	}
}