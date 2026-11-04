using System;
using System.Globalization;

namespace Deepwave.RetroOS.Apps
{
    /// <summary>
    /// The arithmetic behind the Calculator, with no interface. A desk calculator,
    /// not an expression parser: each operator finishes the sum in progress first,
    /// so 2 + 3 * 4 is 20. Kept apart from the view so the sums can be tested
    /// without a canvas or play mode.
    /// </summary>
    public class CalculatorEngine
    {
        double _accumulator;
        double _lastOperand;
        char _pending;              // '\0' when nothing is waiting
        char _lastOperator;         // what = repeats once nothing is pending
        bool _typing;               // digits are extending the entry

        /// <summary>What the readout shows.</summary>
        public string Display { get; private set; } = "0";

        /// <summary>Whatever is in memory. Zero means the M light is off.</summary>
        public double Memory { get; private set; }

        /// <summary>The operator waiting for its right-hand side, or <c>'\0'</c>.</summary>
        public char Pending => _pending;

        /// <summary>Set when a sum failed. Any entry clears it.</summary>
        public string Error { get; private set; }

        /// <summary>The readout as a number.</summary>
        public double Value => double.TryParse(Display, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var v) ? v : 0d;

        public void Digit(char digit)
        {
            if (digit < '0' || digit > '9') return;
            if (Error != null) Clear();

            if (!_typing) { Display = digit == '0' ? "0" : digit.ToString(); _typing = true; }
            else if (Display == "0") Display = digit.ToString();
            else if (Display.Length < 16) Display += digit;
        }

        public void Point()
        {
            if (Error != null) Clear();
            if (!_typing) { Display = "0."; _typing = true; }
            else if (!Display.Contains(".")) Display += ".";
        }

        public void Negate()
        {
            if (Display.StartsWith("-")) Display = Display.Substring(1);
            else if (Display != "0") Display = "-" + Display;
        }

        public void Backspace()
        {
            if (!_typing || Error != null) return;

            Display = Display.Length <= 1 || (Display.Length == 2 && Display.StartsWith("-"))
                ? "0" : Display.Substring(0, Display.Length - 1);
            if (Display == "0") _typing = false;
        }

        /// <summary>Clears the entry, leaving the sum in progress alone.</summary>
        public void ClearEntry()
        {
            Display = "0";
            _typing = false;
            Error = null;
        }

        /// <summary>Clears everything.</summary>
        public void Clear()
        {
            _accumulator = 0d;
            _lastOperand = 0d;
            _pending = '\0';
            _lastOperator = '\0';
            ClearEntry();
        }

        /// <summary>One of <c>+ - * /</c>.</summary>
        public void Operator(char op)
        {
            if (Error != null) return;

            // A second operator with nothing typed between them just swaps
            // which one is waiting rather than folding the sum twice.
            if (_pending != '\0' && _typing) Fold(Value);
            else if (_pending == '\0') _accumulator = Value;

            if (Error != null) return;
            _pending = op;
            _typing = false;
        }

        public void Equals()
        {
            if (Error != null) return;

            // With nothing pending, = repeats the last operation on the readout
            // (2+3 then == gives 8). The era's calculators all did this.
            var repeat = _pending == '\0';
            var op = repeat ? _lastOperator : _pending;
            if (op == '\0') { _typing = false; return; }

            var right = repeat || !_typing ? _lastOperand : Value;
            if (repeat) _accumulator = Value;

            _lastOperator = op;
            _lastOperand = right;
            _pending = op;

            Fold(right);
            _pending = '\0';
            _typing = false;
        }

        public void SquareRoot()
        {
            var value = Value;
            if (value < 0d) { Fail("Invalid input for function"); return; }
            Display = Format(Math.Sqrt(value));
            _typing = false;
        }

        public void Reciprocal()
        {
            var value = Value;
            if (Math.Abs(value) < double.Epsilon) { Fail("Cannot divide by zero"); return; }
            Display = Format(1d / value);
            _typing = false;
        }

        // Percent reads the typed number as a percentage of whatever is
        // waiting on the left, which is what makes 200 + 10 % give 20.
        public void Percent()
        {
            Display = Format(_accumulator * Value / 100d);
            // The result is the right-hand side of the sum in progress, not a
            // finished answer: 200 + 10 % = has to give 220, not 200.
            _typing = true;
        }

        public void MemoryStore() => Memory = Value;
        public void MemoryClear() => Memory = 0d;
        public void MemoryAdd() => Memory += Value;

        public void MemoryRecall()
        {
            Display = Format(Memory);
            _typing = false;
        }

        /// <summary>Puts a number straight on the readout - a paste.</summary>
        public void SetValue(double value)
        {
            Display = Format(value);
            _typing = false;
            Error = null;
        }

        void Fold(double right)
        {
            switch (_pending)
            {
                case '+': _accumulator += right; break;
                case '-': _accumulator -= right; break;
                case '*': _accumulator *= right; break;
                case '/':
                    if (Math.Abs(right) < double.Epsilon) { Fail("Cannot divide by zero"); return; }
                    _accumulator /= right;
                    break;
                default: _accumulator = right; break;
            }

            Display = Format(_accumulator);
        }

        void Fail(string message)
        {
            Error = message;
            Display = "0";
            _typing = false;
            _accumulator = 0d;
            _pending = '\0';
            _lastOperator = '\0';
        }

        // Enough digits to be useful, no trailing zeroes, and never scientific
        // notation on a readout this size.
        public static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            var text = value.ToString("0.##########", CultureInfo.InvariantCulture);
            return text.Length > 16 ? value.ToString("G10", CultureInfo.InvariantCulture) : text;
        }
    }
}
