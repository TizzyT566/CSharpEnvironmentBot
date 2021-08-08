using System;
using System.Collections.Generic;

namespace CSharpEnvironmentBot
{
    public static class Multi
    {
        //public record ActionChain(params Action[] Actions);
        //public record FuncChain(params Func<bool>[] Functions);
        //public record FuncChain<T>(params Func<T>[] Functions);

        //public static ActionChain Try(params Action[] actions) => new(actions);
        //public static FuncChain Try(params Func<bool>[] functions) => new(functions);
        //public static FuncChain<T> Try<T>(params Func<T>[] functions) => new(functions);

        public static List<Exception> Try(params Action[] actions)
        {
            List<Exception> exceptions = new();
            for (int i = 0; i < actions.Length; i++)
            {
                try
                {
                    actions[i]?.Invoke();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            return exceptions;
        }

        public static List<Exception> Try(bool returnOnException, params Action[] actions)
        {
            List<Exception> exceptions = new();
            for (int i = 0; i < actions.Length; i++)
            {
                try
                {
                    actions[i]?.Invoke();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                    if (returnOnException) return exceptions;
                }
            }
            return exceptions;
        }

        public static void Catch(this Exception exception, Action<Exception> exceptionHandler) => exceptionHandler.Invoke(exception);

        public static List<Exception> Catch<T>(this List<Exception> exceptions, Action<T> exceptionHandler) where T : Exception
        {
            for (int i = exceptions.Count - 1; i <= 0; i++)
            {
                if (exceptions[i] != null)
                {

                }
                else if (typeof(T) == exceptions[i].GetType())
                {

                }
            }
            return exceptions;
        }

        public static void Iterate(long to, Action<long> action) => Iterate(0, to, action);
        public static void Iterate(long from, long to, Action<long> action)
        {
            while (from != to)
            {
                action.Invoke(from);
                from += from > to ? -1 : 1;
            }
            action.Invoke(from);
        }

        private static (Func<T>, object)[] FunctionsAndProduce<T>(params Func<T>[] funcs)
        {
            (Func<T>, object)[] result = new (Func<T>, object)[funcs.Length];
            for (int i = 0; i < funcs.Length; i++) result[i] = (funcs[i], null);
            return result;
        }

        public static bool Check<T>(Func<T, bool> condition, bool checkAll, params Func<T>[] functions) => Check(condition, checkAll, out _, FunctionsAndProduce(functions));
        public static bool Check<T, U>(Func<T, bool> condition, bool checkAll, out (T, U)[] results, params (Func<T> func, U produce)[] funcProd)
        {
            bool status = true;
            (T value, U produce)[] res = new (T, U)[funcProd.Length];
            for (int i = 0; i < funcProd.Length; i++)
            {
                (Func<T> func, U produce) = funcProd[i];
                if (func != null)
                {
                    T value = func.Invoke();
                    bool result = condition.Invoke(value);
                    if (checkAll)
                    {
                        res[i] = new(value, produce);
                        if (!result) status = false;
                    }
                    else
                    {
                        if (!result)
                        {
                            results = new (T, U)[] { (value, produce) };
                            return false;
                        }
                    }
                }
            }
            results = res;
            return status;
        }
    }

    public static class ExtensionMethods
    {
        public static T Pipe<T>(this T @this, params Action<T>[] actions)
        {
            foreach (Action<T> act in actions) act.Invoke(@this);
            return @this;
        }
        public static U Pipe<T, U>(this T @this, params Func<object, object>[] functions)
        {
            object obj = @this;
            foreach (Func<object, object> func in functions) obj = func.Invoke(obj);
            return (U)obj;
        }

        public static int IndexOf(this string @this, int startIndex, params string[] strings)
        {
            int idx = 0, start = startIndex;
            bool wild = false;
            if (strings.Length > 0 && strings[0] == null) throw new Exception("First argument cannot be null");
            for (; startIndex < @this.Length;)
            {
                if (@this[startIndex] == strings[idx][0])
                {
                    if (idx == 0) start = startIndex;
                    if (startIndex + strings[idx].Length > @this.Length) break;
                    if (@this.Substring(startIndex, strings[idx].Length) == strings[idx])
                    {
                        wild = false;
                        startIndex += strings[idx].Length;
                        idx++;
                        for (; idx < strings.Length && strings[idx] == null; idx++) wild = true;
                        if (idx == strings.Length) return start;
                        continue;
                    }
                }
                else if (!wild && idx > 0 && char.IsLetterOrDigit(@this[startIndex])) idx = 0;
                startIndex++;
            }
            return -1;
        }
    }
}
