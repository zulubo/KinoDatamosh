//
// Kino/Datamosh - Glitch effect simulating video compression artifacts
//
// Copyright (C) 2016 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using UnityEngine;

namespace Kino
{
    [RequireComponent(typeof(Camera))]
    public class Datamosh : MonoBehaviour
    {
        #region Public properties and methods

        /// Size of compression macroblock.
        public int blockSize {
            get { return Mathf.Max(4, _blockSize) ; }
            set { _blockSize = value; }
        }

        [SerializeField]
        [Tooltip("Size of compression macroblock.")]
        int _blockSize = 16;

        /// Entropy coefficient. The larger value makes the stronger noise.
        public float entropy {
            get { return _entropy; }
            set { _entropy = value; }
        }

        [SerializeField, Range(0, 1)]
        [Tooltip("Entropy coefficient. The larger value makes the stronger noise.")]
        float _entropy = 0.5f;

        /// Contrast of stripe-shaped noise.
        public float noiseContrast {
            get { return _noiseContrast; }
            set { _noiseContrast = value; }
        }

        [SerializeField, Range(0.5f, 4.0f)]
        [Tooltip("Contrast of stripe-shaped noise.")]
        float _noiseContrast = 1;

        /// Scale factor for velocity vectors.
        public float velocityScale {
            get { return _velocityScale; }
            set { _velocityScale = value; }
        }

        [SerializeField, Range(0, 2)]
        [Tooltip("Scale factor for velocity vectors.")]
        float _velocityScale = 0.8f;

        /// Amount of random displacement.
        public float diffusion {
            get { return _diffusion; }
            set { _diffusion = value; }
        }

        [SerializeField, Range(0, 2)]
        [Tooltip("Amount of random displacement.")]
        float _diffusion = 0.4f;

        /// Start glitching.
        public void Glitch()
        {
            Left._sequence = 1;
            Right._sequence = 1;
        }

        /// Stop glitching.
        public void Reset()
        {
            Left._sequence = 0;
            Right._sequence = 0;
        }

        #endregion

        #region Private properties

        Camera cam;

        [SerializeField]
        Shader _shader;

        Material _material;

        private class MoshEye
        {
            public RenderTexture _workBuffer; // working buffer
            public RenderTexture _dispBuffer; // displacement buffer
            
            public int _sequence;
            public int _lastFrame;
        }

        private MoshEye Left = new MoshEye();
        private MoshEye Right = new MoshEye();

        RenderTexture NewWorkBuffer(RenderTexture source)
        {
            return RenderTexture.GetTemporary(source.width, source.height);
        }

        RenderTexture NewDispBuffer(RenderTexture source)
        {
            var rt = RenderTexture.GetTemporary(
                source.width / _blockSize,
                source.height / _blockSize,
                0, RenderTextureFormat.ARGBHalf
            );
            rt.filterMode = FilterMode.Point;
            return rt;
        }

        void ReleaseBuffer(RenderTexture buffer)
        {
            if (buffer != null) RenderTexture.ReleaseTemporary(buffer);
        }

        #endregion

        #region MonoBehaviour functions

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            _material = new Material(Shader.Find("Hidden/Kino/Datamosh"));
            _material.hideFlags = HideFlags.DontSave;

            cam.depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

            Left._sequence = 0;
            Right._sequence = 0;
        }

        void OnDisable()
        {
            Disable(Left);
            Disable(Right);

            
            DestroyImmediate(_material);
            _material = null;
        }

        void Disable(MoshEye eye)
        {
            ReleaseBuffer(eye._workBuffer);
            eye._workBuffer = null;

            ReleaseBuffer(eye._dispBuffer);
            eye._dispBuffer = null;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            _material.SetFloat("_BlockSize", _blockSize);
            _material.SetFloat("_Quality", 1 - _entropy);
            _material.SetFloat("_Contrast", _noiseContrast);
            _material.SetFloat("_Velocity", _velocityScale);
            _material.SetFloat("_Diffusion", _diffusion);

            MoshEye activeEye;

            if(cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono)
            {
                activeEye = Left;
            }
            else
            {
                activeEye = cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left ? Left : Right;
            }

            if (activeEye._sequence == 0)
            {
                // Step 0: no effect, just keep the last frame.

                // Update the working buffer with the current frame.
                ReleaseBuffer(activeEye._workBuffer);
                activeEye._workBuffer = NewWorkBuffer(source);
                Graphics.Blit(source, activeEye._workBuffer);

                // Blit without effect.
                Graphics.Blit(source, destination);
            }
            else if (activeEye._sequence == 1)
            {
                // Step 1: start effect, no moshing.

                // Initialize the displacement buffer.
                ReleaseBuffer(activeEye._dispBuffer);
                activeEye._dispBuffer = NewDispBuffer(source);
                Graphics.Blit(null, activeEye._dispBuffer, _material, 0);

                // Simply blit the working buffer because motion vectors
                // might not be ready (because of camera switching).
                Graphics.Blit(activeEye._dispBuffer, destination);

                activeEye._sequence++;
            }
            else
            {
                // Step 2: apply effect.

                if (Time.frameCount != activeEye._lastFrame)
                {
                    // Update the displaceent buffer.
                    var newDisp = NewDispBuffer(source);
                    Graphics.Blit(activeEye._dispBuffer, newDisp, _material, 1);
                    ReleaseBuffer(activeEye._dispBuffer);
                    activeEye._dispBuffer = newDisp;

                    // Moshing!
                    var newWork = NewWorkBuffer(source);
                    _material.SetTexture("_WorkTex", activeEye._workBuffer);
                    _material.SetTexture("_DispTex", activeEye._dispBuffer);
                    Graphics.Blit(source, newWork, _material, 2);
                    ReleaseBuffer(activeEye._workBuffer);
                    activeEye._workBuffer = newWork;

                    activeEye._lastFrame = Time.frameCount;
                }

                // Blit the result.
                Graphics.Blit(activeEye._workBuffer, destination);
            }
        }

        #endregion
    }
}
