using System;
using UnityEditor;
using UnityEngine;

namespace DesertLantern.Editor
{
    internal sealed class MotelCharacterImport : AssetPostprocessor
    {
        private bool IsMotelCharacter => assetPath.StartsWith("Assets/Resources/Characters/",
            StringComparison.Ordinal) && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsMotelCharacter)
                return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.importBlendShapes = false;
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
                return;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;
            }
            importer.clipAnimations = clips;
        }

        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!IsMotelCharacter)
                return;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

    }
}
