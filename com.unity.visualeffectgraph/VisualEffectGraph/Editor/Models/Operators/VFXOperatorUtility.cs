using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;

namespace UnityEditor.VFX
{
    static class VFXOperatorUtility
    {
        public static Dictionary<VFXValueType, VFXExpression> GenerateExpressionConstant(float baseValue)
        {
            return new Dictionary<VFXValueType, VFXExpression>()
            {
                { VFXValueType.Float, VFXValue.Constant(baseValue) },
                { VFXValueType.Float2, VFXValue.Constant(Vector2.one * baseValue) },
                { VFXValueType.Float3, VFXValue.Constant(Vector3.one * baseValue) },
                { VFXValueType.Float4, VFXValue.Constant(Vector4.one * baseValue) },
                { VFXValueType.Int32, VFXValue.Constant((int)baseValue) },
                { VFXValueType.Uint32, VFXValue.Constant((uint)baseValue) }
            };
        }

        public static readonly Dictionary<VFXValueType, VFXExpression> OneExpression = GenerateExpressionConstant(1.0f);
        public static readonly Dictionary<VFXValueType, VFXExpression> MinusOneExpression = GenerateExpressionConstant(-1.0f);
        public static readonly Dictionary<VFXValueType, VFXExpression> HalfExpression = GenerateExpressionConstant(0.5f);
        public static readonly Dictionary<VFXValueType, VFXExpression> ZeroExpression = GenerateExpressionConstant(0.0f);
        public static readonly Dictionary<VFXValueType, VFXExpression> TwoExpression = GenerateExpressionConstant(2.0f);
        public static readonly Dictionary<VFXValueType, VFXExpression> ThreeExpression = GenerateExpressionConstant(3.0f);
        public static readonly Dictionary<VFXValueType, VFXExpression> PiExpression = GenerateExpressionConstant(Mathf.PI);
        public static readonly Dictionary<VFXValueType, VFXExpression> TauExpression = GenerateExpressionConstant(2.0f * Mathf.PI);
        public static readonly Dictionary<VFXValueType, VFXExpression> E_NapierConstantExpression = GenerateExpressionConstant(Mathf.Exp(1));

        // unified binary op
        static public VFXExpression UnifyOp(Func<VFXExpression, VFXExpression, VFXExpression> f, VFXExpression e0, VFXExpression e1)
        {
            var unifiedExp = VFXOperatorUtility.UpcastAllFloatN(new VFXExpression[2] {e0, e1}).ToArray();
            return f(unifiedExp[0], unifiedExp[1]);
        }

        // unified ternary op
        static public VFXExpression UnifyOp(Func<VFXExpression, VFXExpression, VFXExpression, VFXExpression> f, VFXExpression e0, VFXExpression e1, VFXExpression e2)
        {
            var unifiedExp = VFXOperatorUtility.UpcastAllFloatN(new VFXExpression[3] {e0, e1, e2}).ToArray();
            return f(unifiedExp[0], unifiedExp[1], unifiedExp[2]);
        }

        static public VFXExpression Negate(VFXExpression input)
        {
            var minusOne = VFXOperatorUtility.MinusOneExpression[input.valueType];
            return (minusOne * input);
        }

        static public VFXExpression Clamp(VFXExpression input, VFXExpression min, VFXExpression max)
        {
            //Max(Min(x, max), min))
            var maxExp = new VFXExpressionMax(input, CastFloat(min, input.valueType));
            return new VFXExpressionMin(maxExp, CastFloat(max, input.valueType));
        }

        static public VFXExpression Saturate(VFXExpression input)
        {
            //Max(Min(x, 1.0f), 0.0f))
            return Clamp(input, VFXValue.Constant(0.0f), VFXValue.Constant(1.0f));
        }

        static public VFXExpression Frac(VFXExpression input)
        {
            //x - floor(x)
            return input - new VFXExpressionFloor(input);
        }

        static public VFXExpression Round(VFXExpression input)
        {
            //x = floor(x + 0.5)
            var half = HalfExpression[input.valueType];
            return new VFXExpressionFloor(input + half);
        }

        static public VFXExpression Log(VFXExpression input, VFXExpression _base)
        {
            //log2(x)/log2(b)
            return new VFXExpressionLog2(input) / new VFXExpressionLog2(_base);
        }

        static public VFXExpression Atanh(VFXExpression input)
        {
            //0.5*Log((1+x)/(1-x), e)
            var half = HalfExpression[input.valueType];
            var one = OneExpression[input.valueType];
            var e = E_NapierConstantExpression[input.valueType];

            return half * Log((one + input) / (one - input), e);
        }

        static public VFXExpression SinH(VFXExpression input)
        {
            //0.5*(e^x - e^-x)
            var half = HalfExpression[input.valueType];
            var minusOne = MinusOneExpression[input.valueType];
            var e = E_NapierConstantExpression[input.valueType];

            return half * (new VFXExpressionPow(e, input) - new VFXExpressionPow(e, minusOne * input));
        }

        static public VFXExpression CosH(VFXExpression input)
        {
            //0.5*(e^x + e^-x)
            var half = HalfExpression[input.valueType];
            var minusOne = MinusOneExpression[input.valueType];
            var e = E_NapierConstantExpression[input.valueType];

            return half * (new VFXExpressionPow(e, input) + new VFXExpressionPow(e, minusOne * input));
        }

        static public VFXExpression TanH(VFXExpression input)
        {
            //(1-e^2x)/(1+e^2x)
            var two = TwoExpression[input.valueType];
            var one = OneExpression[input.valueType];
            var minusOne = MinusOneExpression[input.valueType];
            var e = E_NapierConstantExpression[input.valueType];
            var E_minusTwoX = new VFXExpressionPow(e, minusOne * two * input);

            return (one - E_minusTwoX) / (one + E_minusTwoX);
        }

        static public VFXExpression VanDerCorputSequence(VFXExpression bits) //expect an uint return a float
        {
            bits = bits << 16 | bits >> 16;
            bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAA) >> 1);
            bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCC) >> 2);
            bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0) >> 4);
            bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00) >> 8);

            return new VFXExpressionCastUintToFloat(bits) * new VFXValue<float>(2.3283064365386963e-10f); // / 0x100000000;
        }

        static public VFXExpression Sqrt(VFXExpression input)
        {
            //pow(x, 0.5f)
            return new VFXExpressionPow(input, HalfExpression[input.valueType]);
        }

        static public VFXExpression Dot(VFXExpression a, VFXExpression b)
        {
            //a.x*b.x + a.y*b.y + ...
            var size = VFXExpression.TypeToSize(a.valueType);
            if (a.valueType != b.valueType)
            {
                throw new ArgumentException(string.Format("Invalid Dot type input : {0} and {1}", a.valueType, b.valueType));
            }

            var mul = (a * b);
            var sum = new Stack<VFXExpression>();
            if (size == 1)
            {
                sum.Push(mul);
            }
            else
            {
                for (int iChannel = 0; iChannel < size; ++iChannel)
                {
                    sum.Push(mul[iChannel]);
                }
            }

            while (sum.Count > 1)
            {
                var top = sum.Pop();
                var bottom = sum.Pop();
                sum.Push(top + bottom);
            }
            return sum.Pop();
        }

        static public VFXExpression Distance(VFXExpression x, VFXExpression y)
        {
            //length(a - b)
            return Length(x - y);
        }

        static public VFXExpression SqrDistance(VFXExpression x, VFXExpression y)
        {
            //dot(a - b)
            var delta = (x - y);
            return Dot(delta, delta);
        }

        static public VFXExpression Lerp(VFXExpression x, VFXExpression y, VFXExpression s)
        {
            //x + s(y - x)
            return (x + s * (y - x));
        }

        static public VFXExpression Length(VFXExpression v)
        {
            //sqrt(dot(v, v))
            var dot = Dot(v, v);
            return Sqrt(dot);
        }

        static public VFXExpression Normalize(VFXExpression v)
        {
            var invLength = (OneExpression[VFXValueType.Float] / Length(v));
            var invLengthVector = CastFloat(invLength, v.valueType);
            return (v * invLengthVector);
        }

        static public VFXExpression Modulo(VFXExpression x, VFXExpression y)
        {
            if (VFXExpression.IsFloatValueType(x.valueType))
            {
                //fmod : frac(x / y) * y
                return Frac(x / y) * y;
            }
            else
            {
                //Std 1152 If the quotient a/b is representable, the expression (a/b)*b + a%b shall equal a.
                return x - (x / y) * y;
            }
        }

        static public VFXExpression Fit(VFXExpression value, VFXExpression oldRangeMin, VFXExpression oldRangeMax, VFXExpression newRangeMin, VFXExpression newRangeMax)
        {
            //percent = (value - oldRangeMin) / (oldRangeMax - oldRangeMin)
            //lerp(newRangeMin, newRangeMax, percent)
            VFXExpression percent = (value - oldRangeMin) / (oldRangeMax - oldRangeMin);
            return Lerp(newRangeMin, newRangeMax, percent);
        }

        static public VFXExpression Smoothstep(VFXExpression x, VFXExpression y, VFXExpression s)
        {
            var type = x.valueType;

            var t = (s - x) / (y - x);
            t = Clamp(t, ZeroExpression[type], OneExpression[type]);

            var result = (ThreeExpression[type] - TwoExpression[type] * t);

            result = (result * t);
            result = (result * t);

            return result;
        }

        static public VFXExpression Discretize(VFXExpression value, VFXExpression granularity)
        {
            return new VFXExpressionFloor(value / granularity) * granularity;
        }

        static public VFXExpression ColorLuma(VFXExpression color)
        {
            //(0.299*R + 0.587*G + 0.114*B)
            var coefficients = VFXValue.Constant(new Vector4(0.299f, 0.587f, 0.114f, 0.0f));
            return Dot(color, coefficients);
        }

        static public VFXExpression DegToRad(VFXExpression degrees)
        {
            return (degrees * CastFloat(VFXValue.Constant(Mathf.PI / 180.0f), degrees.valueType));
        }

        static public VFXExpression RadToDeg(VFXExpression radians)
        {
            return (radians * CastFloat(VFXValue.Constant(180.0f / Mathf.PI), radians.valueType));
        }

        static public VFXExpression PolarToRectangular(VFXExpression theta, VFXExpression distance)
        {
            //x = cos(angle) * distance
            //y = sin(angle) * distance
            var result = new VFXExpressionCombine(new VFXExpression[] { new VFXExpressionCos(theta), new VFXExpressionSin(theta) });
            return (result * CastFloat(distance, VFXValueType.Float2));
        }

        static public VFXExpression[] RectangularToPolar(VFXExpression coord)
        {
            //theta = atan2(coord.y, coord.x)
            //distance = length(coord)
            var components = VFXOperatorUtility.ExtractComponents(coord).ToArray();
            var theta = new VFXExpressionATan2(components[1], components[0]);
            var distance = Length(coord);
            return new VFXExpression[] { theta, distance };
        }

        static public VFXExpression SphericalToRectangular(VFXExpression theta, VFXExpression phi, VFXExpression distance)
        {
            //x = cos(theta) * cos(phi) * distance
            //y = sin(theta) * cos(phi) * distance
            //z = sin(phi) * distance
            var cosTheta = new VFXExpressionCos(theta);
            var cosPhi = new VFXExpressionCos(phi);
            var sinTheta = new VFXExpressionSin(theta);
            var sinPhi = new VFXExpressionSin(phi);

            var x = (cosTheta * cosPhi);
            var y = sinPhi;
            var z = (sinTheta * cosPhi);

            var result = new VFXExpressionCombine(new VFXExpression[] { x, y, z });
            return (result * CastFloat(distance, VFXValueType.Float3));
        }

        static public VFXExpression[] RectangularToSpherical(VFXExpression coord)
        {
            //distance = length(coord)
            //theta = atan2(z, x)
            //phi = asin(y / distance)
            var components = VFXOperatorUtility.ExtractComponents(coord).ToArray();
            var distance = Length(coord);
            var theta = new VFXExpressionATan2(components[2], components[0]);
            var phi = new VFXExpressionASin(components[1] / distance);
            return new VFXExpression[] { theta, phi, distance };
        }

        static public VFXExpression CircleArea(VFXExpression radius)
        {
            //pi * r * r
            var pi = VFXValue.Constant(Mathf.PI);
            return (pi * radius * radius);
        }

        static public VFXExpression CircleCircumference(VFXExpression radius)
        {
            //2 * pi * r
            var two = VFXValue.Constant(2.0f);
            var pi = VFXValue.Constant(Mathf.PI);
            return (two * pi * radius);
        }

        static public VFXExpression BoxVolume(VFXExpression dimensions)
        {
            //x * y * z
            var components = ExtractComponents(dimensions).ToArray();
            return (components[0] * components[1] * components[2]);
        }

        static public VFXExpression SphereVolume(VFXExpression radius)
        {
            //(4 / 3) * pi * r * r * r
            var multiplier = VFXValue.Constant((4.0f / 3.0f) * Mathf.PI);
            return (multiplier * radius * radius * radius);
        }

        static public VFXExpression CylinderVolume(VFXExpression radius, VFXExpression height)
        {
            //pi * r * r * h
            var pi = VFXValue.Constant(Mathf.PI);
            return (pi * radius * radius * height);
        }

        static public VFXExpression ConeVolume(VFXExpression radius0, VFXExpression radius1, VFXExpression height)
        {
            //pi/3 * (r0 * r0 + r0 * r1 + r1 * r1) * h
            var piOver3 = VFXValue.Constant(Mathf.PI / 3.0f);
            VFXExpression r0r0 = (radius0 * radius0);
            VFXExpression r0r1 = (radius0 * radius1);
            VFXExpression r1r1 = (radius1 * radius1);
            VFXExpression result = (r0r0 + r0r1 + r1r1);
            return (piOver3 * result * height);
        }

        static public VFXExpression TorusVolume(VFXExpression majorRadius, VFXExpression minorRadius)
        {
            //(pi * r * r) * (2 * pi * R)
            return CircleArea(minorRadius) * CircleCircumference(majorRadius);
        }

        static public VFXExpression SignedDistanceToPlane(VFXExpression planePosition, VFXExpression planeNormal, VFXExpression position)
        {
            VFXExpression d = Dot(planePosition, planeNormal);
            return Dot(position, planeNormal) - d;
        }

        static public VFXExpression GammaToLinear(VFXExpression gamma)
        {
            var components = VFXOperatorUtility.ExtractComponents(gamma).ToArray();
            if (components.Length != 3 && components.Length != 4)
                throw new ArgumentException("input expression must be a 3 or 4 components vector");

            VFXExpression exp = VFXValue.Constant(2.2f);
            for (int i = 0; i < 3; ++i)
                components[i] = new VFXExpressionPow(components[i], exp);

            return new VFXExpressionCombine(components);
        }

        static public VFXExpression LinearToGamma(VFXExpression linear)
        {
            var components = VFXOperatorUtility.ExtractComponents(linear).ToArray();
            if (components.Length != 3 && components.Length != 4)
                throw new ArgumentException("input expression must be a 3 or 4 components vector");

            VFXExpression exp = VFXValue.Constant(1.0f / 2.2f);
            for (int i = 0; i < 3; ++i)
                components[i] = new VFXExpressionPow(components[i], exp);

            return new VFXExpressionCombine(components);
        }

        static public IEnumerable<VFXExpression> ExtractComponents(VFXExpression expression)
        {
            if (expression.valueType == VFXValueType.Float)
            {
                return new[] { expression };
            }

            var components = new List<VFXExpression>();
            for (int i = 0; i < VFXExpression.TypeToSize(expression.valueType); ++i)
            {
                components.Add(expression[i]);
            }
            return components;
        }

        static public VFXValueType FindMaxFloatNValueType(IEnumerable<VFXExpression> inputExpression)
        {
            return inputExpression.Select(o => o.valueType).OrderBy(t => VFXExpression.IsFloatValueType(t) ? VFXExpression.TypeToSize(t) : 0).Last();
        }

        static public IEnumerable<VFXExpression> UpcastAllFloatN(IEnumerable<VFXExpression> inputExpression, float defaultValue = 0.0f)
        {
            if (inputExpression.Count() <= 1)
            {
                return inputExpression;
            }

            var maxValueType = FindMaxFloatNValueType(inputExpression);
            var newVFXExpression = inputExpression.Select(o => VFXExpression.IsFloatValueType(o.valueType) ? CastFloat(o, maxValueType, defaultValue) : o);
            return newVFXExpression.ToArray();
        }

        static public VFXExpression CastFloat(VFXExpression from, VFXValueType toValueType, float defaultValue = 0.0f)
        {
            if (!VFXExpressionNumericOperation.IsFloatValueType(from.valueType) || !VFXExpressionNumericOperation.IsFloatValueType(toValueType))
            {
                throw new ArgumentException(string.Format("Invalid CastFloat : {0} to {1}", from, toValueType));
            }

            if (from.valueType == toValueType)
            {
                return from;
            }

            var fromValueType = from.valueType;
            var fromValueTypeSize = VFXExpression.TypeToSize(fromValueType);
            var toValueTypeSize = VFXExpression.TypeToSize(toValueType);

            var inputComponent = new VFXExpression[fromValueTypeSize];
            var outputComponent = new VFXExpression[toValueTypeSize];

            if (inputComponent.Length == 1)
            {
                inputComponent[0] = from;
            }
            else
            {
                for (int iChannel = 0; iChannel < fromValueTypeSize; ++iChannel)
                {
                    inputComponent[iChannel] = from[iChannel];
                }
            }

            for (int iChannel = 0; iChannel < toValueTypeSize; ++iChannel)
            {
                if (iChannel < fromValueTypeSize)
                {
                    outputComponent[iChannel] = inputComponent[iChannel];
                }
                else if (fromValueTypeSize == 1)
                {
                    //Manage same logic behavior for float => floatN in HLSL
                    outputComponent[iChannel] = inputComponent[0];
                }
                else
                {
                    outputComponent[iChannel] = VFXValue.Constant(defaultValue);
                }
            }

            if (toValueTypeSize == 1)
            {
                return outputComponent[0];
            }

            var combine = new VFXExpressionCombine(outputComponent);
            return combine;
        }

        static public VFXExpression FixedRandom(uint hash, bool perElement)
        {
            return FixedRandom(VFXValue.Constant<uint>(hash), perElement);
        }

        static public VFXExpression FixedRandom(VFXExpression hash, bool perElement)
        {
            VFXExpression seed = new VFXExpressionBitwiseXor(hash, VFXBuiltInExpression.SystemSeed);
            return new VFXExpressionFixedRandom(seed, perElement);
        }
    }
}
