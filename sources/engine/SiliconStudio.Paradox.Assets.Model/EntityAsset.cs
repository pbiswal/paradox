﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using SiliconStudio.Assets;
using SiliconStudio.Assets.Compiler;
using SiliconStudio.Core;
using SiliconStudio.Paradox.EntityModel.Data;

namespace SiliconStudio.Paradox.Assets.Model
{
    [DataContract("Entity")]
    [AssetFileExtension(FileExtension)]
    [AssetCompiler(typeof(EntityAssetCompiler))]
    [ThumbnailCompiler(PreviewerCompilerNames.EntityThumbnailCompilerQualifiedName)]
    [AssetFactory(typeof(EntityFactory))]
    [AssetDescription("Entity", "An entity", true)]
    public class EntityAsset : Asset
    {
        /// <summary>
        /// The default file extension used by the <see cref="EntityAsset"/>.
        /// </summary>
        public const string FileExtension = ".pdxentity";

        public EntityAsset()
        {
            Data = new EntityData();
        }

        // Not used in current version but later it should allow extracting lights, cameras, etc... as children entities.
        // Or maybe it would be better as separate LightsAsset and CamerasAsset?
        //[DataMember(10)]
        //public UPath Source { get; set; }

        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        [DataMember(20)]
        public EntityData Data { get; set; }

        private class EntityFactory : IAssetFactory
        {
            public Asset New()
            {
                return new EntityAsset();
            }
        }
    }
}