using UnityEngine;

[ExecuteAlways]
public class SkyboxShaderController : MonoBehaviour
{
    [SerializeField] private Transform _sunTransform;
    [SerializeField] private Transform _moonTransform;

    private void LateUpdate()
    {
        Shader.SetGlobalVector("_MoonDirection", _moonTransform.forward);
        Shader.SetGlobalMatrix("_MoonSpaceMatrix", new Matrix4x4(-_moonTransform.forward, _moonTransform.up, -_moonTransform.right, Vector4.zero).transpose);
    }
}
