Shader "Tagtag/PlaneHatch"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Input
            {
                float4 vertex : POSITION;
            };
            struct Interpolated
            {
                float4 position : SV_POSITION;
                float2 planeMeters : TEXCOORD0;
            };

            Interpolated vert(Input input)
            {
                Interpolated output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.planeMeters = input.vertex.xz;
                return output;
            }

            fixed4 frag(Interpolated input) : SV_Target
            {
                // Coordinates are metres in the ARPlane's local space, so rescan changes
                // the clipped polygon without shifting the pencil strokes.
                float stroke = (input.planeMeters.x + input.planeMeters.y) / 0.033;
                float rough = 0.012 * sin(input.planeMeters.x * 173.0 + input.planeMeters.y * 71.0);
                float distanceFromStroke = abs(frac(stroke + rough) - 0.5);
                float feather = max(fwidth(stroke), 0.012);
                float ink = 1.0 - smoothstep(0.045, 0.045 + feather, distanceFromStroke);
                float grain = 0.86 + 0.14 * sin(input.planeMeters.x * 391.0 - input.planeMeters.y * 207.0);
                return fixed4(1.0, 0.79, 0.14, 0.085 + 0.28 * ink * grain);
            }
            ENDCG
        }
    }
}
