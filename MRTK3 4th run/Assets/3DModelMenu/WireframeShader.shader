Shader "Custom/Wireframe"
{
    Properties
    {
        _Color ("Wireframe Color", Color) = (1,1,1,1)
        _Width ("Wireframe Width", Range(0.001, 0.1)) = 0.01
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
            };
            
            fixed4 _Color;
            float _Width;
            float _Alpha;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _Color;
                col.a = _Alpha;
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}