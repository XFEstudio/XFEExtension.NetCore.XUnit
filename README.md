# XFE各类拓展.NetCore.XUnit

## 描述

XUnit测试框架，使得用户不需要编写Main函数，只需要在需要测试的类或者方法上面加上相对于的特性即可。

## 示例

### 快速编写无参测试用例（CTest和MTest）

```csharp
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

---

### 带参数的测试用例

```csharp
using XFE各类拓展.NetCore.XUnit;

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

### 使用断言（通过继承类）

```csharp
using XFE各类拓展.NetCore.XUnit;

[CTest]
public class TestClass : XFECode
{
    [MTest(1, 2)]
    [MTest(2, 3)]
    public void TestMethod(int a, int b)
    {
        Assert(a + b == 3, "不等于3");
    }
}
```

---

### 使用断言（不继承）

```csharp
using XFE各类拓展.NetCore.XUnit;

[CTest]
public class TestClass
{
    [MTest(1, 2)]
    [MTest(2, 3)]
    public void TestMethod(int a, int b)
    {
        XFECode.Assert(a + b == 3, "不等于3");
    }
}
```

---

### 判断返回值是否相等（MRTest）

```csharp
using XFE各类拓展.NetCore.XUnit;

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

### 为测试用例添加描述（CNTest和MNTest）

```csharp
using XFE各类拓展.NetCore.XUnit;

[CTest("这是一个测试类")]
public class TestClass
{
    [MNTest("这是一个测试方法")]
    public void TestMethod()
    {
        Console.WriteLine("Hello World!");
    }
}
```

---

### 同时添加描述和结果对比（MNRTest）

```csharp
using XFE各类拓展.NetCore.XUnit;

[CTest("这是一个测试类")]
public class TestClass
{
    [MNRTest("这是一个测试方法", 1, 2, 3)]
    public int TestMethod(int a, int b)
    {
        return a + b;
    }
}
```

---

### 设置测试类的初始化方法（SetUp）

```csharp
using XFE各类拓展.NetCore.XUnit;

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

### 直接测试静态方法(SMTest)

```csharp
using XFE各类拓展.NetCore.XUnit;

public class TestClass
{
    [SMTest]
    public static void TestMethod()
    {
        Console.WriteLine("Hello World!");
    }
}
```