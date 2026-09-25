Shader "Tagtag/DeviceSticker"
{
    Properties
    {
        _MainTex ("Sticker", 2D) = "white" {}
        _Finish ("Finish", Float) = 0
        _Pattern ("Foil pattern", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "StickerSurface.cginc"
            sampler2D _MainTex;
            float _Finish,_Pattern;
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 tilt : TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;
                float3 view=normalize(ObjSpaceViewDir(v.vertex));o.tilt=view.xy;return o;
            }
            fixed4 frag(v2f i) : SV_Target
            { return StickerSurface(tex2D(_MainTex,i.uv),i.uv,_Finish,_Pattern,i.tilt,0); }
            ENDCG
        }
    }
}
