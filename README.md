# XFE各类拓展.NetCore.XUnit

## 描述

XUnit测试框架，使得用户不需要编写Main函数，只需要在需要测试的类或者方法上面加上相对于的特性即可。

## 示例

#### 快速编写无参测试用例

```csharp
using System;
using XFE各类拓展.NetCore.XUnit;

[CTest]
//[CTest]
//可添加多个测试用例
public class TestClass
{
	[MTest]
	//[MTest]
	//[MTest]
	//可添加多个测试用例
	public void TestMethod()
	{
		Console.WriteLine("Hello World!");
	}
}
```

#### 带参数的测试用例

```csharp
using System;
using XFE各类拓展.NetCore.XUnit;

[CTest]
public class TestClass
{
	[MTest]
	[MTest(1, 2)]
	[MTest(2, 3)]
	[MTest(3, 4)]
	public void TestMethod(int a, int b)
	{
		Console.WriteLine(a + b);
	}
}
```