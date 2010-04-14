//===================================================================================
// Microsoft patterns & practices
// Composite Application Guidance for Windows Presentation Foundation and Silverlight
//===================================================================================
// Copyright (c) Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===================================================================================
// The example companies, organizations, products, domain names,
// e-mail addresses, logos, people, places, and events depicted
// herein are fictitious.  No association with any real company,
// organization, product, domain name, email address, logo, person,
// places, or events is intended or should be inferred.
//===================================================================================
using System;
using System.Collections.Generic;
using Microsoft.Practices.Composite.Regions;

namespace Microsoft.Practices.Composite.Presentation.Regions
{
    /// <summary>
    /// Interface for RegionBehaviorFactories. This factory allows the registration of the default set of RegionBehaviors, that will
    /// be added to the <see cref="IRegionBehaviorCollection"/>s of all <see cref="IRegion"/>s, unless overridden on a 'per-region' basis. 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "It is more of a factory than a collection")]
    public interface IRegionBehaviorFactory : IEnumerable<string>
    {
        /// <summary>
        /// Adds a particular type of RegionBehavior if it was not already registered. the <paramref name="behaviorKey"/> string is used to check if the behavior is already present
        /// </summary>
        /// <param name="behaviorKey">The behavior key that's used to find if a certain behavior is already added.</param>
        /// <param name="behaviorType">Type of the behavior to add. .</param>
        void AddIfMissing(string behaviorKey, Type behaviorType);

        /// <summary>
        /// Determines whether a behavior with the specified key already exists
        /// </summary>
        /// <param name="behaviorKey">The behavior key.</param>
        /// <returns>
        /// <see langword="true"/> if a behavior with the specified key is present; otherwise, <see langword="false"/>.
        /// </returns>
        bool ContainsKey(string behaviorKey);

        /// <summary>
        /// Creates an instance of the the Behaviortype that's registered using the specified key.
        /// </summary>
        /// <param name="key">The key that's used to register a behavior type.</param>
        /// <returns>The created behavior. </returns>
        IRegionBehavior CreateFromKey(string key);
    }
}