//-----------------------------------------------------------------------
// <copyright file="AccountManager.cs" company="Sitecore Corporation">
//     Copyright (c) Sitecore Corporation 1999-2017
// </copyright>
// <summary>Defines the CE specific AccountManager class.</summary>
//-----------------------------------------------------------------------
// Copyright 2017 Sitecore Corporation A/S
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//       http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
// -------------------------------------------------------------------------------------------

using Sitecore.Commerce.XA.Foundation.Common.Context;
using Sitecore.Commerce.XA.Foundation.Common.Utils;

namespace Sitecore.Support.Commerce.XA.Foundation.CommerceEngine.Managers
{
    using Diagnostics;
    using Sitecore.Commerce.Engine.Connect.Entities;
    using Sitecore.Commerce.Entities;
    using Sitecore.Commerce.Entities.Customers;
    using Sitecore.Commerce.Services.Carts;
    using Sitecore.Commerce.Services.Customers;
    using Sitecore.Commerce.XA.Foundation.Common.Models;
    using Sitecore.Commerce.XA.Foundation.Connect;
    using Sitecore.Commerce.XA.Foundation.Connect.Entities;
    using Sitecore.Commerce.XA.Foundation.Connect.Managers;
    using Sitecore.Commerce.XA.Foundation.Connect.Providers;
    using Sitecore.Commerce.XA.Foundation.CommerceEngine.ExtensionMethods;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Defines the CE specific cart manager implementation.
    /// </summary>
    /// <seealso cref="Sitecore.Commerce.XA.Foundation.Connect.Managers.AccountManager" />
    public class AccountManager : Foundation.Connect.Managers.AccountManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccountManager"/> class.
        /// </summary>
        /// <param name="connectServiceProvider">The connect service provider.</param>
        /// <param name="cartManager">The cart manager.</param>
        /// <param name="storefrontContext">The storefront context.</param>
        /// <param name="modelProvider">The model provider.</param>
        public AccountManager([NotNull] IConnectServiceProvider connectServiceProvider, ICartManager cartManager, [NotNull] IStorefrontContext storefrontContext, [NotNull] IModelProvider modelProvider)
            : base(connectServiceProvider, cartManager, storefrontContext, modelProvider)
        {
        }

        /// <summary>
        /// Adds the address.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="address">The address.</param>
        /// <returns>
        /// The manager response where the add parties result is returned in the response.
        /// </returns>
        public override ManagerResponse<Sitecore.Commerce.Services.Customers.AddPartiesResult, bool> AddAddress([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext, PartyEntity address)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");
            Assert.ArgumentNotNull(address, "address");

            var result = new Sitecore.Commerce.Services.Customers.AddPartiesResult { Success = false };

            var getUserResponse = this.GetUser(visitorContext.UserName);
            if (!getUserResponse.ServiceProviderResult.Success || getUserResponse.Result == null)
            {
                result.SystemMessages.ToList().AddRange(getUserResponse.ServiceProviderResult.SystemMessages);
                return new ManagerResponse<Sitecore.Commerce.Services.Customers.AddPartiesResult, bool>(result, false);
            }

            var commerceCustomer = new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId };

            var party = address.ToCommerceParty();

            var request = new Sitecore.Commerce.Services.Customers.AddPartiesRequest(commerceCustomer, new List<Party> { party });
            result = this.CustomerServiceProvider.AddParties(request);

            Helpers.LogSystemMessages(result.SystemMessages, result);

            return new ManagerResponse<Sitecore.Commerce.Services.Customers.AddPartiesResult, bool>(result, result.Success);
        }

        /// <summary>
        /// Updates the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="address">The address.</param>
        /// <returns>
        /// The manager response where the add parties result is returned in the response.
        /// </returns>
        public override ManagerResponse<CustomerResult, bool> UpdateAddress([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext, PartyEntity address)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");
            Assert.ArgumentNotNull(address, "address");

            var getUserResponse = this.GetUser(visitorContext.UserName);
            if (!getUserResponse.ServiceProviderResult.Success || getUserResponse.Result == null)
            {
                var customerResult = new CustomerResult { Success = false };
                customerResult.SystemMessages.ToList().AddRange(getUserResponse.ServiceProviderResult.SystemMessages);
                return new ManagerResponse<CustomerResult, bool>(customerResult, false);
            }

            var customer = new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId };
            var party = address.ToCommerceParty();

            var request = new Sitecore.Commerce.Services.Customers.UpdatePartiesRequest(customer, new List<Party> { party });
            var result = this.CustomerServiceProvider.UpdateParties(request);

            if (!result.Success)
            {
                Helpers.LogSystemMessages(result.SystemMessages, result);
            }

            return new ManagerResponse<CustomerResult, bool>(result, result.Success);
        }

        /// <summary>
        /// Gets the current customer addresses.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <returns>
        /// The manager response with the customer addresses.
        /// </returns>
        public override ManagerResponse<GetPartiesResult, IEnumerable<PartyEntity>> GetCurrentCustomerAddresses([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");

            var result = new GetPartiesResult { Success = false };
            var getUserResponse = this.GetUser(visitorContext.UserName);

            if (!getUserResponse.ServiceProviderResult.Success || getUserResponse.Result == null)
            {
                result.SystemMessages.ToList().AddRange(getUserResponse.ServiceProviderResult.SystemMessages);
                return new ManagerResponse<GetPartiesResult, IEnumerable<PartyEntity>>(result, null);
            }

            var customer = new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId };
            var request = new GetPartiesRequest(customer);
            result = this.CustomerServiceProvider.GetParties(request);
            var partyList = result.Success && result.Parties != null ? (result.Parties).Cast<CommerceParty>() : new List<CommerceParty>();
            var partyEntityList = partyList.ToPartyEntityList();

            Helpers.LogSystemMessages(result.SystemMessages, result);
            return new ManagerResponse<GetPartiesResult, IEnumerable<PartyEntity>>(result, partyEntityList);
        }

        /// <summary>
        /// Removes the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="user">The user.</param>
        /// <param name="parties">The parties.</param>
        /// <returns>The manager result where the success flag is returned as the Result.</returns>
        public override ManagerResponse<CustomerResult, bool> RemoveParties([NotNull] CommerceStorefront storefront, [NotNull] CommerceCustomer user, List<Party> parties)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(user, "user");
            Assert.ArgumentNotNull(parties, "parties");

            var request = new Sitecore.Commerce.Services.Customers.RemovePartiesRequest(user, parties);
            var result = this.CustomerServiceProvider.RemoveParties(request);

            if (!result.Success)
            {
                Helpers.LogSystemMessages(result.SystemMessages, result);
            }

            return new ManagerResponse<CustomerResult, bool>(result, result.Success);
        }

        /// <summary>
        /// Deletes the address.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="addressId">The address identifier.</param>
        /// <returns>
        /// The manager response where the delete parties result is returned in the response.
        /// </returns>
        public override ManagerResponse<CustomerResult, bool> DeleteAddress([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext, string addressId)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");
            Assert.ArgumentNotNullOrEmpty(addressId, "addressId");

            var getUserResponse = this.GetUser(visitorContext.UserName);
            if (!getUserResponse.ServiceProviderResult.Success || getUserResponse.Result == null)
            {
                var customerResult = new CustomerResult { Success = false };
                customerResult.SystemMessages.ToList().AddRange(getUserResponse.ServiceProviderResult.SystemMessages);
                return new ManagerResponse<CustomerResult, bool>(customerResult, false);
            }

            var customer = new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId };
            var parties = new List<Party> { new Party { ExternalId = addressId } };

            return this.RemoveParties(storefront, customer, parties);
        }
    }
}