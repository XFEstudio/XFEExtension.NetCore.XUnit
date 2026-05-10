# XFEExtension.NetCore.XUnit

[![NuGet](https://img.shields.io/nuget/v/XFEExtension.NetCore.XUnit?label=NuGet&logo=NuGet)](https://www.nuget.org/packages/XFEExtension.NetCore.XUnit/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/XFEExtension.NetCore.XUnit?label=Downloads&logo=NuGet)](https://www.nuget.org/packages/XFEExtension.NetCore.XUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download)

> 📖 English | [简体中文](https://github.com/XFEstudio/XFEExtension.NetCore.XUnit/blob/master/README.zh-CN.md)


## Description

XFEExtension.NetCore.XUnit is an XUnit-based testing framework that lets you run tests without writing a `Main` method. Simply annotate the classes or methods you want to test with the provided attributes.

## Examples

### Quick parameterless test cases (CTest & MTest)

```csharp
[CTest]
//[CTest]
// Multiple test cases can be added
public class TestClass
{
    [MTest]
    //[MTest]
    //[MTest]
    // Multiple test cases can be added
    public void TestMethod()
    {
        Console.WriteLine("Hello World!");
    }
}
```

---

### Test cases with parameters

```csharp
[CTest]
public class TestClass
{
    [MTest(1, 2)]
    [MTest(2, 3)]
    [MTest(3, 4)]
    public void TestMethod(int a, int b)
    {
        Console.WriteLine(a + b);
    }
}
```

---

### Assertions (via inheritance)

```csharp
[CTest]
public class TestClass : XFECode
{
    [MTest(1, 2)]
    [MTest(2, 3)]
    public void TestMethod(int a, int b)
    {
        Assert(a + b == 3, "Not equal to 3");
    }
}
```

---

### Assertions (without inheritance)

```csharp
[CTest]
public class TestClass
{
    [MTest(1, 2)]
    [MTest(2, 3)]
    public void TestMethod(int a, int b)
    {
        XFECode.Assert(a + b == 3, "Not equal to 3");
    }
}
```

---

### Verify return value equality (MRTest)

```csharp
[CTest]
public class TestClass
{
    [MRTest(1, 2, 3)]
    [MRTest(2, 3, 5)]
    [MRTest(3, 4, 7)]
    public int TestMethod(int a, int b)
    {
        return a + b;
    }
}
```

---

### Add descriptions to test cases (CNTest & MNTest)

```csharp
[CTest("This is a test class")]
public class TestClass
{
    [MNTest("This is a test method")]
    public void TestMethod()
    {
        Console.WriteLine("Hello World!");
    }
}
```

---

### Add description and return-value comparison together (MNRTest)

```csharp
[CTest("This is a test class")]
public class TestClass
{
    [MNRTest("This is a test method", 1, 2, 3)]
    public int TestMethod(int a, int b)
    {
        return a + b;
    }
}
```

---

### Set an initialization method for the test class (SetUp)

```csharp
[CTest]
public class TestClass
{
    string initWord;

    [SetUp]
    public void SetUp()
    {
        initWord = "Hello World!";
    }

    [MTest]
    public void TestMethod()
    {
        Console.WriteLine(initWord);
    }
}
```

---

### Test static methods directly (SMTest)

```csharp
public class TestClass
{
    [SMTest]
    public static void TestMethod()
    {
        Console.WriteLine("Hello World!");
    }
}
```
