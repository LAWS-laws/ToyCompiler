# ToyCompiler

> **A small compiler and register-based virtual machine written entirely in C#.**

ToyCompiler 是一个使用 C# 编写的实验性编译器与虚拟机项目。

它包含一套类似 C# / Java 的高级语言、一个将源代码编译为固定长度字节码的编译器，以及一个负责解释执行这些字节码的寄存器式虚拟机。

与 JVM 字节码或 .NET IL 不同，ToyCompiler 的指令主要直接操作寄存器，而不是通过操作数栈完成计算。

## 特点

* **Register-based Virtual Machine**
* **54 条自定义字节码指令**
* 固定长度的字节码指令
* 完全使用解释器执行，不生成本地机器码
* 使用 C# 实现，无需第三方库
* 类似 C# / Java 的源代码语法
* 支持类、引用变量、数组和多维数组
* 支持函数重载
* 支持基本的条件与控制流语句
* 编译完成后可以查看生成的字节码、栈空间以及常量等信息

## 语言

ToyCompiler 的源代码语言是一种较为简单的高级语言，语法主要参考 C# 和 Java。

目前支持以下基本数据类型：

```text
char  float  int  byte  bool
```

其中 `char` 占用 2 字节。

语言还支持引用变量、数组、多维数组、类、函数重载以及基本的条件控制语句。

### 示例

一个最简单的 ToyCompiler 程序如下：

```csharp
void Main() /*EntryPoint*/
{
    Print("HelloWorld!");
}
```

更多示例可以在 [`Doc/SourceDemo.txt`](Doc/Source.java) 中找到。

## 虚拟机与字节码

ToyCompiler 使用一套包含 **54 条指令**的寄存器式指令集。

指令覆盖了整数和浮点数运算、比较、逻辑运算、类型转换、条件跳转、函数调用、栈操作、指针操作以及动态内存分配等功能。

例如：

```text
addi R1 R2 R3
```

表示：

```text
R1 = R2 + R3
```

也就是说，指令的操作数直接来自寄存器，而不是从操作数栈中取得。

### 一个简单的编译示例

以下 ToyCompiler 源代码：

```csharp
int a=0;
int b=6;
a = a+(b*2);
Print(a);
```

会被编译成类似下面的字节码：

```text
2    lod     a       0
3    lod     b       6
4    lod     _A      2
5    muli    _A      b       _A
6    addi    _A      a       _A
7    set     a       _A
8    push    a
9    call    Print
```

可以看到：

```text
muli    _A      b       _A
对应 _A = b * _A
```

随后：

```text
addi    _A      a       _A
对应 _A = a + _A
```

最终：

```text
set     a       _A
将计算结果写回变量a
```


这个例子也体现了 ToyCompiler 与典型栈式虚拟机的区别：算术指令直接指定参与运算的寄存器。

## 指令集

目前的 54 条指令大致可以分为：

| 类型    | 示例                                       |
| ----- | ---------------------------------------- |
| 整数运算  | `addi` `subi` `muli` `divi` `modi`       |
| 整数比较  | `gtri` `smri` `egtri` `esmri`            |
| 浮点运算  | `addf` `subf` `mulf` `divf` `modf`       |
| 浮点比较  | `gtrf` `smrf` `egtrf` `esmrf`            |
| 逻辑运算  | `and` `or`                               |
| 类型转换  | `i2s` `i2b` `i2f` `f2i`                  |
| 控制流   | `cjmp` `jump` `call` `ret` `ret0` `stop` |
| 栈操作   | `push` `pop` `stsp`                      |
| 指针与内存 | `getpz` `setpz` `getp4` `setp4` 等        |
| 内存分配  | `malloc` `salloc`                        |

## 编译与执行

ToyCompiler 的基本工作流程为：

```text
源代码
  │
  ▼
预处理
  │
  ▼
编译器（编译与链接）
  │
  ▼
固定长度字节码
  │
  ▼
虚拟机
  │
  ▼
解释执行
```

编译完成后，程序会输出生成的字节码以及相关的运行时信息，例如栈空间和常量等，然后由虚拟机直接解释执行。

虚拟机仍然使用栈来保存**函数调用信息以及寄存器状态**，但具体的指令运算采用寄存器模型。

## 关于构建

### 环境要求

* **.NET 8 SDK**
* **x86（32 位）运行环境**

> **重要：当前版本必须以 x86 架构运行，不支持 x64。**

ToyCompiler 中的虚拟机寄存器固定为 **4 字节**大小，而指针也需要存储在这些寄存器中。因此，64 位指针无法在当前的寄存器设计中正确表示。

使用 Visual Studio 构建时，请将解决方案平台设置为：

```text
x86
```

不要使用 `Any CPU` 或 `x64`。

## 项目状态

ToyCompiler 是一个我在空余时间实现的编译器、字节码以及虚拟机项目，并非面向生产环境的编程语言或编译器。

语言设计和指令集设计主要服务于项目本身的实验目的，因此当前实现可能存在 Bug 以及其他限制。
