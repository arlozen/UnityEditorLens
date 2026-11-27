using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace EditorLens
{
    internal static class ElementContentReader
    {
        internal static ElementContentSnapshot Read(VisualElement element, bool redactContent)
        {
            if (redactContent)
            {
                return Redacted();
            }

            if (element == null)
            {
                return new ElementContentSnapshot
                {
                    Status = ContentCaptureStatus.Failed,
                    Diagnostic = "元素为空，无法读取内容。"
                };
            }

            if (ShouldRedactSubtree(element))
            {
                return Redacted();
            }

            try
            {
                // 严格匹配内置类型，避免调用自定义派生控件覆盖的 getter。
                var elementType = element.GetType();
                if (elementType == typeof(TextElement)
                    || elementType == typeof(Label)
                    || elementType == typeof(Button))
                {
                    return new ElementContentSnapshot
                    {
                        Status = ContentCaptureStatus.Captured,
                        Text = ((TextElement)element).text
                    };
                }

                if (elementType == typeof(TextField))
                {
                    return CapturedValue(typeof(string), ((TextField)element).value);
                }

                if (elementType == typeof(Toggle))
                {
                    return CapturedValue(typeof(bool), ((Toggle)element).value.ToString(CultureInfo.InvariantCulture));
                }

                if (elementType == typeof(IntegerField))
                {
                    return CapturedValue(typeof(int), ((IntegerField)element).value.ToString(CultureInfo.InvariantCulture));
                }

                if (elementType == typeof(LongField))
                {
                    return CapturedValue(typeof(long), ((LongField)element).value.ToString(CultureInfo.InvariantCulture));
                }

                if (elementType == typeof(FloatField))
                {
                    return CapturedValue(typeof(float), ((FloatField)element).value.ToString("R", CultureInfo.InvariantCulture));
                }

                if (elementType == typeof(DoubleField))
                {
                    return CapturedValue(typeof(double), ((DoubleField)element).value.ToString("R", CultureInfo.InvariantCulture));
                }

                if (elementType == typeof(Slider))
                {
                    return CapturedValue(typeof(float), ((Slider)element).value.ToString("R", CultureInfo.InvariantCulture));
                }

                if (elementType == typeof(SliderInt))
                {
                    return CapturedValue(typeof(int), ((SliderInt)element).value.ToString(CultureInfo.InvariantCulture));
                }

                if (elementType == typeof(EnumField))
                {
                    var value = ((EnumField)element).value;
                    return CapturedValue(value == null ? typeof(Enum) : value.GetType(), value?.ToString("G"));
                }

                if (elementType == typeof(DropdownField))
                {
                    return CapturedValue(typeof(string), ((DropdownField)element).value);
                }

                return new ElementContentSnapshot
                {
                    Status = ContentCaptureStatus.Unsupported,
                    Diagnostic = "此具体控件类型不在内容读取白名单中。"
                };
            }
            catch (Exception exception)
            {
                return new ElementContentSnapshot
                {
                    Status = ContentCaptureStatus.Failed,
                    Diagnostic = "读取内置控件内容失败：" + exception.GetType().FullName
                };
            }
        }

        internal static bool ShouldRedactSubtree(VisualElement element)
        {
            try
            {
                // 外层密码标志是非虚属性；基类引用也绕过派生类型的同名 new 属性。
                // 仅匹配使用已知内置输入实现的字段，避免自定义内部输入覆盖 getter。
                if (element is TextField textField)
                    return ((TextInputBaseField<string>)textField).isPasswordField;
                if (element is IntegerField integerField)
                    return ((TextInputBaseField<int>)integerField).isPasswordField;
                if (element is LongField longField)
                    return ((TextInputBaseField<long>)longField).isPasswordField;
                if (element is FloatField floatField)
                    return ((TextInputBaseField<float>)floatField).isPasswordField;
                if (element is DoubleField doubleField)
                    return ((TextInputBaseField<double>)doubleField).isPasswordField;

                return false;
            }
            catch (Exception)
            {
                // 无法确认密码状态时保守脱敏，且不读取任何文本或值。
                return true;
            }
        }

        private static ElementContentSnapshot CapturedValue(Type valueType, string value)
        {
            return new ElementContentSnapshot
            {
                Status = ContentCaptureStatus.Captured,
                ValueType = valueType.FullName,
                Value = value
            };
        }

        private static ElementContentSnapshot Redacted()
        {
            return new ElementContentSnapshot
            {
                Status = ContentCaptureStatus.Redacted,
                Diagnostic = "密码输入区域的文本和值已脱敏。"
            };
        }
    }
}
