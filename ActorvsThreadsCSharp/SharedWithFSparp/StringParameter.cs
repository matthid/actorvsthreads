namespace Yaaf.Utils.Helper
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// This util class helps to convert text into multiple parameters and vice versa
    /// </summary>
    public class StringParameter
    {
        private readonly string parameter;

        private readonly char escapeChar;

        private readonly char separationChar;

        private List<string> parameters;

        private readonly string escapeString;

        private readonly string separationString;

        public string Parameter
        {
            get
            {
                return this.parameter;
            }
        }
        public string this[int index]
        {
            get
            {
                this.SetParameters();
                return this.parameters[index];
            }
        }
        public IEnumerable<string> Parameters
        {
            get
            {
                this.SetParameters();
                return this.parameters;
            }
        }

        public int ParameterCount
        {
            get
            {
                this.SetParameters();
                return this.parameters.Count;
            }
        }

        private void SetParameters()
        {
            if (this.parameters == null)
            {
                var line = this.Parameter;
                if (string.IsNullOrEmpty(line))
                {
                    this.parameters = new List<string>();
                    return;
                }
                var links = new List<string>();
                bool ignoreNext = false;
                var current = new StringBuilder();
                foreach (var c in line)
                {
                    if (!ignoreNext)
                    {
                        if (c == this.escapeChar)
                        {
                            ignoreNext = true;
                        }
                        if (c == this.separationChar)
                        {
                            links.Add(current.ToString());
#if NET2
                            current = new StringBuilder();
#else
                            current.Clear();
#endif
                            continue;
                        }
                    }
                    else
                    {
                        ignoreNext = false;
                    }

                    current.Append(c);
                }

                links.Add(current.ToString());
                this.parameters = links.Select(this.UnEscapeString).ToList();
            }
        }

        private string UnEscapeString(string toUnescape)
        {
            return toUnescape.Replace(this.escapeString + this.separationChar, this.separationString).Replace(this.escapeString + this.escapeChar, this.escapeString);
        }

        public static string UnEscapeString(string toUnescape, char escapeChar, char seperationChar)
        {
            return new StringParameter(escapeChar, seperationChar).UnEscapeString(toUnescape);
        }

        private string EscapeString(string toEscape)
        {
            return toEscape.Replace(this.escapeString, this.escapeString + this.escapeChar).Replace(this.separationString, this.escapeString + this.separationChar);
        }

        public static string EscapeString(string toUnescape, char escapeChar, char seperationChar)
        {
            return new StringParameter(escapeChar, seperationChar).EscapeString(toUnescape);
        }

        private StringParameter(char escapeChar = '/', char separationChar = ',')
        {
            if (escapeChar == separationChar)
            {
                throw new ArgumentException("The Seperation and Escape Characters have  to be different", "escapeChar");
            }
            this.escapeChar = escapeChar;
            this.escapeString = this.escapeChar.ToString(CultureInfo.InvariantCulture);
            this.separationChar = separationChar;
            this.separationString = this.separationChar.ToString(CultureInfo.InvariantCulture);
        }

        public StringParameter(string parameter, char escapeChar = '/', char separationChar = ',')
            : this (escapeChar, separationChar)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException("parameter");
            }

            this.parameter = parameter;
        }

        public StringParameter(IEnumerable<string> parameters, char escapeChar = '/', char separationChar = ',')
            : this(escapeChar, separationChar)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            this.parameter = string.Join(this.separationString, parameters.Select(this.EscapeString)
#if NET2
                .ToArray()
#endif
                );
        }

        public override string ToString()
        {
            return string.Format("{0}", this.parameter);
        }

        public static implicit operator StringParameter(string parameter)
        {
            return parameter == null ? null : new StringParameter(parameter);
        }
        public static implicit operator string(StringParameter parameter)
        {
            return parameter == null ? null : parameter.Parameter;
        }
    }
}