using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace XFEExtension.NetCore.XUnit.Assertions;

/// <summary>
/// 表示 XFE 强类型断言未满足时产生的测试失败。
/// </summary>
/// <param name="message">描述断言期望值与实际值差异的消息。</param>
public sealed class XfeAssertionException(string message) : Exception(message);

/// <summary>
/// 提供不依赖全局状态的强类型测试断言；断言失败时抛出 <see cref="XfeAssertionException"/>。
/// </summary>
public static class Assert
{
    /// <summary>
    /// 验证条件为 <see langword="true"/>。
    /// </summary>
    /// <param name="condition">要验证的布尔条件。</param>
    /// <param name="message">断言失败时使用的可选消息。</param>
    /// <exception cref="XfeAssertionException"><paramref name="condition"/> 为 <see langword="false"/>。</exception>
    public static void True([DoesNotReturnIf(false)] bool condition, string? message = null)
    {
        if (!condition)
            throw new XfeAssertionException(message ?? "Expected true, but found false.");
    }

    /// <summary>
    /// 验证条件为 <see langword="false"/>。
    /// </summary>
    /// <param name="condition">要验证的布尔条件。</param>
    /// <param name="message">断言失败时使用的可选消息。</param>
    /// <exception cref="XfeAssertionException"><paramref name="condition"/> 为 <see langword="true"/>。</exception>
    public static void False([DoesNotReturnIf(true)] bool condition, string? message = null)
    {
        if (condition)
            throw new XfeAssertionException(message ?? "Expected false, but found true.");
    }

    /// <summary>
    /// 验证期望值与实际值相等；对于非字符串可枚举值，会按顺序逐项比较。
    /// </summary>
    /// <typeparam name="T">参与比较的值类型。</typeparam>
    /// <param name="expected">期望值。</param>
    /// <param name="actual">实际值。</param>
    /// <param name="message">断言失败时使用的可选消息。</param>
    /// <exception cref="XfeAssertionException">两个值不相等，或两个序列的长度或元素不同。</exception>
    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (expected is IEnumerable expectedItems && actual is IEnumerable actualItems && expected is not string && actual is not string)
        {
            var expectedEnumerator = expectedItems.GetEnumerator();
            var actualEnumerator = actualItems.GetEnumerator();
            var index = 0;
            try
            {
                while (true)
                {
                    var hasExpected = expectedEnumerator.MoveNext();
                    var hasActual = actualEnumerator.MoveNext();
                    if (!hasExpected && !hasActual)
                        return;
                    if (hasExpected != hasActual || !object.Equals(expectedEnumerator.Current, actualEnumerator.Current))
                    {
                        var expectedValue = hasExpected ? Format(expectedEnumerator.Current) : "<end of collection>";
                        var actualValue = hasActual ? Format(actualEnumerator.Current) : "<end of collection>";
                        throw new XfeAssertionException(message ?? $"Collections differ at index {index}. Expected {expectedValue}, actual {actualValue}.");
                    }
                    index++;
                }
            }
            finally
            {
                (expectedEnumerator as IDisposable)?.Dispose();
                (actualEnumerator as IDisposable)?.Dispose();
            }
        }
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new XfeAssertionException(message ?? $"Expected: {Format(expected)}{Environment.NewLine}Actual:   {Format(actual)}");
    }

    /// <summary>
    /// 验证指定值不等于不期望出现的值。
    /// </summary>
    /// <typeparam name="T">参与比较的值类型。</typeparam>
    /// <param name="notExpected">不期望出现的值。</param>
    /// <param name="actual">实际值。</param>
    /// <param name="message">断言失败时使用的可选消息。</param>
    /// <exception cref="XfeAssertionException">两个值相等。</exception>
    public static void NotEqual<T>(T notExpected, T actual, string? message = null)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
            throw new XfeAssertionException(message ?? $"Did not expect: {Format(actual)}");
    }

    /// <summary>
    /// 验证值为 <see langword="null"/>。
    /// </summary>
    /// <param name="value">要验证的值。</param>
    /// <param name="message">断言失败时使用的可选消息。</param>
    /// <exception cref="XfeAssertionException"><paramref name="value"/> 不为 <see langword="null"/>。</exception>
    public static void Null(object? value, string? message = null)
    {
        if (value is not null)
            throw new XfeAssertionException(message ?? $"Expected null, but found {Format(value)}.");
    }

    /// <summary>
    /// 验证值不为 <see langword="null"/>，并向可空流分析器声明成功后的非空状态。
    /// </summary>
    /// <param name="value">要验证的值。</param>
    /// <param name="message">断言失败时使用的可选消息。</param>
    /// <exception cref="XfeAssertionException"><paramref name="value"/> 为 <see langword="null"/>。</exception>
    public static void NotNull([NotNull] object? value, string? message = null)
    {
        if (value is null)
            throw new XfeAssertionException(message ?? "Expected a non-null value.");
    }

    /// <summary>
    /// 验证两个值引用同一个对象实例。
    /// </summary>
    /// <param name="expected">期望引用的对象。</param>
    /// <param name="actual">实际引用的对象。</param>
    /// <exception cref="XfeAssertionException">两个引用不指向同一个对象。</exception>
    public static void Same(object? expected, object? actual)
    {
        if (!ReferenceEquals(expected, actual))
            throw new XfeAssertionException("Expected both values to reference the same object.");
    }

    /// <summary>
    /// 验证序列中至少包含一个与期望值相等的元素。
    /// </summary>
    /// <typeparam name="T">序列元素类型。</typeparam>
    /// <param name="expected">期望包含的元素。</param>
    /// <param name="values">要搜索的元素序列。</param>
    /// <exception cref="XfeAssertionException">序列中不存在期望元素。</exception>
    public static void Contains<T>(T expected, IEnumerable<T> values)
    {
        if (!values.Contains(expected))
            throw new XfeAssertionException($"Collection did not contain {Format(expected)}.");
    }

    /// <summary>
    /// 使用区分大小写的序号比较验证字符串包含指定子串。
    /// </summary>
    /// <param name="expectedSubstring">期望出现的子串。</param>
    /// <param name="actual">要搜索的完整字符串。</param>
    /// <exception cref="XfeAssertionException">实际字符串不包含期望子串。</exception>
    public static void Contains(string expectedSubstring, string actual)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new XfeAssertionException($"String did not contain {Format(expectedSubstring)}.");
    }

    /// <summary>
    /// 验证序列恰好包含一个元素并返回该元素。
    /// </summary>
    /// <typeparam name="T">序列元素类型。</typeparam>
    /// <param name="values">要验证的序列。</param>
    /// <returns>序列中的唯一元素。</returns>
    /// <exception cref="XfeAssertionException">序列为空或包含多个元素。</exception>
    public static T Single<T>(IEnumerable<T> values)
    {
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new XfeAssertionException("Expected one item, but the collection was empty.");
        var item = enumerator.Current;
        if (enumerator.MoveNext())
            throw new XfeAssertionException("Expected one item, but the collection contained multiple items.");
        return item;
    }

    /// <summary>
    /// 对序列中的每个元素执行给定断言，并在失败消息中包含元素索引。
    /// </summary>
    /// <typeparam name="T">序列元素类型。</typeparam>
    /// <param name="values">要逐项验证的序列。</param>
    /// <param name="assertion">应用于每个元素的断言委托。</param>
    /// <exception cref="XfeAssertionException">任一元素的断言失败。</exception>
    public static void All<T>(IEnumerable<T> values, Action<T> assertion)
    {
        var index = 0;
        foreach (var value in values)
        {
            try
            {
                assertion(value);
            }
            catch (Exception exception)
            {
                throw new XfeAssertionException($"Assertion failed for item {index}: {exception.Message}");
            }
            index++;
        }
    }

    /// <summary>
    /// 验证可枚举对象不包含任何元素。
    /// </summary>
    /// <param name="values">要验证的可枚举对象。</param>
    /// <exception cref="XfeAssertionException">枚举器能够读取到至少一个元素。</exception>
    public static void Empty(IEnumerable values)
    {
        var enumerator = values.GetEnumerator();
        try
        {
            if (enumerator.MoveNext())
                throw new XfeAssertionException("Expected an empty collection.");
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 验证值的运行时类型与指定类型完全一致并返回转换后的值。
    /// </summary>
    /// <typeparam name="T">期望的精确运行时类型。</typeparam>
    /// <param name="value">要检查的值。</param>
    /// <returns>转换为 <typeparamref name="T"/> 的值。</returns>
    /// <exception cref="XfeAssertionException">值为空、不可转换或其运行时类型不是 <typeparamref name="T"/>。</exception>
    public static T IsType<T>(object? value)
    {
        if (value is not T typed || value.GetType() != typeof(T))
            throw new XfeAssertionException($"Expected exact type {typeof(T)}, but found {value?.GetType()}.");
        return typed;
    }

    /// <summary>
    /// 验证值可赋给指定类型并返回转换后的值。
    /// </summary>
    /// <typeparam name="T">期望可赋值到的类型。</typeparam>
    /// <param name="value">要检查的值。</param>
    /// <returns>转换为 <typeparamref name="T"/> 的值。</returns>
    /// <exception cref="XfeAssertionException">值不能赋给 <typeparamref name="T"/>。</exception>
    public static T AssignableFrom<T>(object? value)
    {
        if (value is not T typed)
            throw new XfeAssertionException($"Expected a value assignable to {typeof(T)}, but found {value?.GetType()}.");
        return typed;
    }

    /// <summary>
    /// 验证同步委托抛出指定类型的异常并返回该异常。
    /// </summary>
    /// <typeparam name="TException">期望捕获的异常类型。</typeparam>
    /// <param name="action">应抛出异常的同步委托。</param>
    /// <returns>委托抛出的 <typeparamref name="TException"/> 实例。</returns>
    /// <exception cref="XfeAssertionException">委托没有抛出异常，或抛出了其他类型的异常。</exception>
    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new XfeAssertionException($"Expected {typeof(TException).Name}, but caught {exception.GetType().Name}.");
        }
        throw new XfeAssertionException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    /// <summary>
    /// 完整等待异步委托，并验证其抛出指定类型的异常。
    /// </summary>
    /// <typeparam name="TException">期望捕获的异常类型。</typeparam>
    /// <param name="action">应异步抛出异常的 <see cref="Task"/> 委托。</param>
    /// <returns>表示异步断言的任务；结果为捕获到的异常实例。</returns>
    /// <exception cref="XfeAssertionException">委托没有抛出异常，或抛出了其他类型的异常。</exception>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new XfeAssertionException($"Expected {typeof(TException).Name}, but caught {exception.GetType().Name}.");
        }
        throw new XfeAssertionException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    /// <summary>
    /// 完整等待异步委托，并验证其抛出指定类型的异常。
    /// </summary>
    /// <typeparam name="TException">期望捕获的异常类型。</typeparam>
    /// <param name="action">应异步抛出异常的 <see cref="ValueTask"/> 委托。</param>
    /// <returns>表示异步断言的值任务；结果为捕获到的异常实例。</returns>
    /// <exception cref="XfeAssertionException">委托没有抛出异常，或抛出了其他类型的异常。</exception>
    public static async ValueTask<TException> ThrowsAsync<TException>(Func<ValueTask> action) where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new XfeAssertionException($"Expected {typeof(TException).Name}, but caught {exception.GetType().Name}.");
        }
        throw new XfeAssertionException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    /// <summary>
    /// 验证可比较值位于包含上下边界的闭区间内。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IComparable{T}"/> 的值类型。</typeparam>
    /// <param name="actual">要检查的实际值。</param>
    /// <param name="low">允许的最小值。</param>
    /// <param name="high">允许的最大值。</param>
    /// <exception cref="XfeAssertionException">实际值小于下界或大于上界。</exception>
    public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
    {
        if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
            throw new XfeAssertionException($"Expected {Format(actual)} to be in [{Format(low)}, {Format(high)}].");
    }

    private static string Format(object? value) => value?.ToString() ?? "<null>";
}
