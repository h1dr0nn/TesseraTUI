using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tessera.Core.Native;

/// <summary>
/// P/Invoke wrapper for Rust formula engine (Tessera.Native)
/// Provides safe C# interface to native formula calculations
/// </summary>
public static class FormulaNative
{
    private const string NativeLibName = "tessera_native"; // Will be tessera_native.dll/.dylib/.so

    private static bool _isAvailable = true; // Assume available until proven otherwise

    /// <summary>
    /// Check if native library is available and can be loaded
    /// </summary>
    public static bool IsAvailable => _isAvailable;

    [StructLayout(LayoutKind.Sequential)]
    private struct FormulaResultNative
    {
        public double Value;
        public IntPtr Error; // null if success, pointer to C string if error
    }

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FormulaResultNative tessera_sum(
        [MarshalAs(UnmanagedType.LPStr)] string columnName,
        IntPtr valuesPtr,
        int count);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FormulaResultNative tessera_avg(
        [MarshalAs(UnmanagedType.LPStr)] string columnName,
        IntPtr valuesPtr,
        int count);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FormulaResultNative tessera_min(
        [MarshalAs(UnmanagedType.LPStr)] string columnName,
        IntPtr valuesPtr,
        int count);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FormulaResultNative tessera_max(
        [MarshalAs(UnmanagedType.LPStr)] string columnName,
        IntPtr valuesPtr,
        int count);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FormulaResultNative tessera_count(
        [MarshalAs(UnmanagedType.LPStr)] string columnName,
        IntPtr valuesPtr,
        int count);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr tessera_parse_formula([MarshalAs(UnmanagedType.LPStr)] string formula);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void tessera_free_string(IntPtr ptr);

    /// <summary>
    /// Calculate SUM for a column of values
    /// </summary>
    /// <param name="columnName">Name of the column (for future column-based references)</param>
    /// <param name="values">Array of string values to sum</param>
    /// <returns>Sum result or error message</returns>
    public static (double? Value, string? Error) Sum(string columnName, IReadOnlyList<string?> values)
    {
        return CallNativeFormulaFunction(tessera_sum, columnName, values, FallbackSum);
    }

    /// <summary>
    /// Parse formula string (e.g., "=SUM(ColumnA)") into function name and arguments
    /// </summary>
    /// <param name="formula">Formula string</param>
    /// <returns>Parsed result as "FUNCTION:ColumnName" or error message</returns>
    public static (string? Result, string? Error) ParseFormula(string formula)
    {
        if (!_isAvailable || string.IsNullOrWhiteSpace(formula))
        {
            return ParseFormulaFallback(formula);
        }

        try
        {
            var resultPtr = tessera_parse_formula(formula);
            if (resultPtr == IntPtr.Zero)
            {
                return (null, "Failed to parse formula");
            }

            var result = Marshal.PtrToStringAnsi(resultPtr);
            tessera_free_string(resultPtr);

            // Check if result is an error (starts with common error keywords)
            if (result != null && (result.StartsWith("Formula must", StringComparison.OrdinalIgnoreCase) ||
                                   result.StartsWith("Invalid", StringComparison.OrdinalIgnoreCase) ||
                                   result.StartsWith("Null", StringComparison.OrdinalIgnoreCase)))
            {
                return (null, result);
            }

            return (result, null);
        }
        catch (DllNotFoundException)
        {
            _isAvailable = false;
            return ParseFormulaFallback(formula);
        }
        catch (Exception)
        {
            return ParseFormulaFallback(formula);
        }
    }

    /// <summary>
    /// Calculate AVG (average) for a column of values
    /// </summary>
    public static (double? Value, string? Error) Avg(string columnName, IReadOnlyList<string?> values)
    {
        return CallNativeFormulaFunction(tessera_avg, columnName, values, FallbackAvg);
    }

    /// <summary>
    /// Calculate MIN for a column of values
    /// </summary>
    public static (double? Value, string? Error) Min(string columnName, IReadOnlyList<string?> values)
    {
        return CallNativeFormulaFunction(tessera_min, columnName, values, FallbackMin);
    }

    /// <summary>
    /// Calculate MAX for a column of values
    /// </summary>
    public static (double? Value, string? Error) Max(string columnName, IReadOnlyList<string?> values)
    {
        return CallNativeFormulaFunction(tessera_max, columnName, values, FallbackMax);
    }

    /// <summary>
    /// Calculate COUNT for a column of values (counts non-null, non-empty values)
    /// </summary>
    public static (double? Value, string? Error) Count(string columnName, IReadOnlyList<string?> values)
    {
        return CallNativeFormulaFunction(tessera_count, columnName, values, FallbackCount);
    }

    /// <summary>
    /// Helper method to call native formula functions with error handling
    /// </summary>
    private static (double? Value, string? Error) CallNativeFormulaFunction(
        Func<string, IntPtr, int, FormulaResultNative> nativeFunc,
        string columnName,
        IReadOnlyList<string?> values,
        Func<string, IReadOnlyList<string?>, (double? Value, string? Error)> fallbackFunc)
    {
        if (!_isAvailable)
        {
            return fallbackFunc(columnName, values);
        }

        try
        {
            var valuePtrs = new List<IntPtr>();
            var cStrings = new List<GCHandle>();

            try
            {
                foreach (var value in values)
                {
                    var cStr = value != null
                        ? Marshal.StringToHGlobalAnsi(value)
                        : IntPtr.Zero;
                    valuePtrs.Add(cStr);
                    if (cStr != IntPtr.Zero)
                    {
                        cStrings.Add(GCHandle.Alloc(cStr, GCHandleType.Pinned));
                    }
                }

                var ptrArray = Marshal.AllocHGlobal(valuePtrs.Count * IntPtr.Size);
                try
                {
                    for (int i = 0; i < valuePtrs.Count; i++)
                    {
                        Marshal.WriteIntPtr(ptrArray, i * IntPtr.Size, valuePtrs[i]);
                    }

                    var result = nativeFunc(columnName, ptrArray, valuePtrs.Count);

                    if (result.Error != IntPtr.Zero)
                    {
                        var errorMsg = Marshal.PtrToStringAnsi(result.Error);
                        tessera_free_string(result.Error);
                        return (null, errorMsg);
                    }

                    return (result.Value, null);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptrArray);
                }
            }
            finally
            {
                foreach (var handle in cStrings)
                {
                    handle.Free();
                }
                foreach (var ptr in valuePtrs)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }
        }
        catch (DllNotFoundException)
        {
            _isAvailable = false;
            return fallbackFunc(columnName, values);
        }
        catch (Exception)
        {
            return fallbackFunc(columnName, values);
        }
    }

    /// <summary>
    /// Fallback C# implementation if Rust library is not available
    /// </summary>
    private static (double? Value, string? Error) FallbackSum(string columnName, IReadOnlyList<string?> values)
    {
        double sum = 0.0;
        int parsedCount = 0;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                sum += num;
                parsedCount++;
            }
        }

        if (parsedCount == 0)
        {
            return (null, "No numeric values found in column");
        }

        return (sum, null);
    }

    /// <summary>
    /// Fallback C# implementation for AVG
    /// </summary>
    private static (double? Value, string? Error) FallbackAvg(string columnName, IReadOnlyList<string?> values)
    {
        double sum = 0.0;
        int parsedCount = 0;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                sum += num;
                parsedCount++;
            }
        }

        if (parsedCount == 0)
        {
            return (null, "No numeric values found in column");
        }

        return (sum / parsedCount, null);
    }

    /// <summary>
    /// Fallback C# implementation for MIN
    /// </summary>
    private static (double? Value, string? Error) FallbackMin(string columnName, IReadOnlyList<string?> values)
    {
        double? minValue = null;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                minValue = minValue.HasValue ? Math.Min(minValue.Value, num) : num;
            }
        }

        if (!minValue.HasValue)
        {
            return (null, "No numeric values found in column");
        }

        return (minValue.Value, null);
    }

    /// <summary>
    /// Fallback C# implementation for MAX
    /// </summary>
    private static (double? Value, string? Error) FallbackMax(string columnName, IReadOnlyList<string?> values)
    {
        double? maxValue = null;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                maxValue = maxValue.HasValue ? Math.Max(maxValue.Value, num) : num;
            }
        }

        if (!maxValue.HasValue)
        {
            return (null, "No numeric values found in column");
        }

        return (maxValue.Value, null);
    }

    /// <summary>
    /// Fallback C# implementation for COUNT
    /// </summary>
    private static (double? Value, string? Error) FallbackCount(string columnName, IReadOnlyList<string?> values)
    {
        int count = 0;

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                count++;
            }
        }

        return (count, null);
    }

    /// <summary>
    /// Fallback C# parser if Rust library is not available
    /// </summary>
    private static (string? Result, string? Error) ParseFormulaFallback(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return (null, "Formula is empty");
        }

        formula = formula.Trim();
        if (!formula.StartsWith('='))
        {
            return (null, "Formula must start with '='");
        }

        var body = formula.Substring(1).Trim();
        var funcEnd = body.IndexOf('(');
        if (funcEnd < 0)
        {
            return (null, "Invalid formula syntax: expected function(arg)");
        }

        if (!body.EndsWith(')'))
        {
            return (null, "Formula missing closing parenthesis");
        }

        var funcName = body.Substring(0, funcEnd).Trim().ToUpperInvariant();
        var args = body.Substring(funcEnd + 1, body.Length - funcEnd - 2).Trim();

        return ($"{funcName}:{args}", null);
    }
}

