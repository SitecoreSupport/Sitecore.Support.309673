//-----------------------------------------------------------------------
// <copyright file="AccountManager.cs" company="Sitecore Corporation">
//     Copyright (c) Sitecore Corporation 1999-2017
// </copyright>
// <summary>Defines the AccountManager class.</summary>
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

namespace Sitecore.Support.Commerce.XA.Foundation.Connect.Managers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using System.Web.Security;
    using Analytics;
    using Diagnostics;
    using Security.Authentication;
    using Sitecore.Commerce.Entities;
    using Sitecore.Commerce.Entities.Customers;
    using Sitecore.Commerce.Services;
    using Sitecore.Commerce.Services.Customers;
    using Sitecore.Commerce.XA.Foundation.Common.Context;
    using Sitecore.Commerce.XA.Foundation.Common.ExtensionMethods;
    using Sitecore.Commerce.XA.Foundation.Common.Models;
    using Sitecore.Commerce.XA.Foundation.Common.Utils;
    using Sitecore.Commerce.XA.Foundation.Connect;
    using Sitecore.Commerce.XA.Foundation.Connect.Entities;
    using Sitecore.Commerce.XA.Foundation.Connect.Managers;
    using Sitecore.Commerce.XA.Foundation.Connect.Providers;
    using static Sitecore.Commerce.XA.Foundation.Common.Constants;

    /// <summary>
    /// Connect service layer account manager implementation.
    /// </summary>
    /// <seealso cref="Sitecore.Commerce.XA.Foundation.Connect.Managers.IAccountManager" />
    public class AccountManager : IAccountManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AccountManager"/> class.
        /// </summary>
        /// <param name="connectServiceProvider">The connect service provider.</param>
        /// <param name="cartManager">The cart manager.</param>
        /// <param name="storefrontContext">The storefront context.</param>
        /// <param name="modelProvider">The model provider.</param>
        public AccountManager([NotNull] IConnectServiceProvider connectServiceProvider, ICartManager cartManager, [NotNull] IStorefrontContext storefrontContext, [NotNull] IModelProvider modelProvider)
        {
            Assert.ArgumentNotNull(connectServiceProvider, "connectServiceProvider");
            Assert.ArgumentNotNull(cartManager, "cartManager");
            Assert.ArgumentNotNull(storefrontContext, "storefrontContext");
            Assert.ArgumentNotNull(modelProvider, "modelProvider");

            this.CustomerServiceProvider = connectServiceProvider.GetCustomerServiceProvider();
            this.CartManager = cartManager;
            this.StorefrontContext = storefrontContext;
            this.ModelProvider = modelProvider;
        }

        /// <summary>
        /// Gets or sets the model provider.
        /// </summary>
        /// <value>
        /// The model provider.
        /// </value>
        public IModelProvider ModelProvider
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the storefront context.
        /// </summary>
        /// <value>
        /// The storefront context.
        /// </value>
        public IStorefrontContext StorefrontContext
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the cart manager.
        /// </summary>
        /// <value>
        /// The cart manager.
        /// </value>
        public ICartManager CartManager
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the customer service provider.
        /// </summary>
        /// <value>
        /// The customer service provider.
        /// </value>
        public CustomerServiceProvider CustomerServiceProvider
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the user using Connect.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <returns>
        /// The manager response containing the result of the service call.
        /// </returns>
        public virtual ManagerResponse<GetUserResult, CommerceUser> GetUser(string userName)
        {
            Assert.ArgumentNotNullOrEmpty(userName, "userName");

            var request = new GetUserRequest(userName);
            var result = this.CustomerServiceProvider.GetUser(request);
            if (!result.Success || result.CommerceUser == null)
            {
                var errorMessage = this.StorefrontContext.GetSystemMessage(AccountConstants.SystemMessages.UserNotFoundError);
                result.SystemMessages.Add(new SystemMessage { Message = errorMessage });
            }

            return new ManagerResponse<GetUserResult, CommerceUser>(result, result.CommerceUser);
        }

        /// <summary>
        /// Logins the specified storefront.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="persistent">if set to <c>true</c> [persistent].</param>
        /// <returns>True if the user is successfully logged in; Otherwise false.</returns>
        public virtual bool Login([NotNull] IStorefrontContext storefront, [NotNull] IVisitorContext visitorContext, string userName, string password, bool persistent)
        {
            Assert.ArgumentNotNullOrEmpty(userName, "userName");
            Assert.ArgumentNotNullOrEmpty(password, "password");

            var anonymousVisitorId = visitorContext.CustomerId;
            var isLoggedIn = AuthenticationManager.Login(userName, password, persistent);

            if (isLoggedIn)
            {
                var anonymousVisitorCart = this.CartManager.GetCurrentCart(visitorContext, storefront).Result;
                Tracker.Current.CheckForNull().Session.IdentifyAs(Sitecore.Commerce.Constants.ContactSource, userName);

                // TODO: Check if valid in SC 9.
                // Tracker.Current.Session.Identify(userName);
                // visitorContext.SetCommerceUser(this.ResolveCommerceUser().Result);
                visitorContext.UserJustLoggedIn();

                this.CartManager.MergeCarts(storefront.CurrentStorefront, visitorContext, anonymousVisitorId, anonymousVisitorCart);
            }

            return isLoggedIn;
        }

        /// <summary>
        /// Registers the user.
        /// </summary>
        /// <param name="storefrontContext">The storefront context.</param>
        /// <param name="userName">Username of the new suer being registered.</param>
        /// <param name="password">Password of the new suer being registered.</param>
        /// <param name="email">The email.</param>
        /// <returns>
        /// Result of the registeration attempt
        /// </returns>
        public virtual ManagerResponse<CreateUserResult, CommerceUser> RegisterUser([NotNull] IStorefrontContext storefrontContext, string userName, string password, string email)
        {
            Assert.ArgumentNotNull(storefrontContext, "storefrontContext");
            Assert.ArgumentNotNullOrEmpty(userName, "userName");
            Assert.ArgumentNotNullOrEmpty(password, "password");

            CreateUserResult result;

            // Attempt to register the user
            try
            {
                var request = new CreateUserRequest(userName, password, email, storefrontContext.CurrentStorefront.ShopName);
                result = this.CustomerServiceProvider.CreateUser(request);

                if (!result.Success)
                {
                    Helpers.LogSystemMessages(result.SystemMessages, result);
                }
                else if (result.Success && result.CommerceUser == null && result.SystemMessages.Count == 0)
                {
                    // TODO: Connect bug:  This is a work around to a Connect bug.  When the user already exists,connect simply aborts the pipeline but
                    // does not set the success flag nor does it return an error message.
                    result.Success = false;
                    result.SystemMessages.Add(new SystemMessage { Message = storefrontContext.GetSystemMessage(AccountConstants.SystemMessages.UserAlreadyExists) });
                }
            }
            catch (MembershipCreateUserException e)
            {
                result = new CreateUserResult { Success = false };
                result.SystemMessages.Add(new SystemMessage { Message = this.ErrorCodeToString(storefrontContext, e.StatusCode) });
            }
            catch (Exception)
            {
                result = new CreateUserResult { Success = false };
                result.SystemMessages.Add(new SystemMessage { Message = storefrontContext.GetSystemMessage(AccountConstants.SystemMessages.UnknownMembershipProviderError) });
            }

            return new ManagerResponse<CreateUserResult, CommerceUser>(result, result.CommerceUser);
        }

        /// <summary>
        /// Logouts this instance.
        /// </summary>
        public virtual void Logout()
        {
            Tracker.Current.CheckForNull().EndVisit(true);
            System.Web.HttpContext.Current.Session.Abandon();
            AuthenticationManager.Logout();
        }

        /// <summary>
        /// Gets the current customer parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <returns>
        /// The manager response with the customer parties.
        /// </returns>
        public virtual ManagerResponse<GetPartiesResult, IEnumerable<Party>> GetCurrentCustomerParties([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(visitorContext, "visitorContext");

            var result = new GetPartiesResult { Success = false };
            var getUserResponse = this.GetUser(visitorContext.UserName);
            if (!getUserResponse.ServiceProviderResult.Success || getUserResponse.Result == null)
            {
                return new ManagerResponse<GetPartiesResult, IEnumerable<Party>>(result, null);
            }

            return this.GetParties(storefront, new CommerceCustomer { ExternalId = getUserResponse.Result.ExternalId });
        }

        /// <summary>
        /// Gets the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="customer">The user.</param>
        /// <returns>The manager response where the list of parties is returned in the response.</returns>
        public virtual ManagerResponse<GetPartiesResult, IEnumerable<Party>> GetParties([NotNull] CommerceStorefront storefront, [NotNull] CommerceCustomer customer)
        {
            Assert.ArgumentNotNull(storefront, "storefront");
            Assert.ArgumentNotNull(customer, "user");

            var request = new GetPartiesRequest(customer);
            var result = this.CustomerServiceProvider.GetParties(request);
            var partyList = result.Success && result.Parties != null ? (result.Parties).Cast<Party>() : new List<Party>();

            Helpers.LogSystemMessages(result.SystemMessages, result);
            return new ManagerResponse<GetPartiesResult, IEnumerable<Party>>(result, partyList);
        }

        /// <summary>
        /// Resets the user password.
        /// </summary>
        /// <param name="emailAddress">Email Address/Username of the user.</param>
        /// <param name="emailSubject">The email subject.</param>
        /// <param name="emailBody">The email body.</param>
        /// <returns>
        /// Result of the forgot password attempt
        /// </returns>
        public virtual ManagerResponse<UpdatePasswordResult, bool> ResetUserPassword(string emailAddress, string emailSubject, string emailBody)
        {
            Assert.ArgumentNotNullOrEmpty(emailAddress, "emailAddress");
            Assert.ArgumentNotNullOrEmpty(emailBody, "emailBody");

            var emailSent = false;
            var result = new UpdatePasswordResult { Success = true };

            try
            {
                var getUserResponse = this.GetUser(emailAddress);

                if (!getUserResponse.ServiceProviderResult.Success || getUserResponse.Result == null)
                {
                    result.Success = false;

                    foreach (var systemMessage in getUserResponse.ServiceProviderResult.SystemMessages)
                    {
                        result.SystemMessages.Add(systemMessage);
                    }
                }
                else
                {
                    var userIpAddress = HttpContext.Current != null ? HttpContext.Current.Request.UserHostAddress : string.Empty;
                    var userName = Membership.Provider.GetUserNameByEmail(getUserResponse.Result.Email);
                    string provisionalPassword = Membership.Provider.ResetPassword(userName, string.Empty);

                    var mailUtil = new MailUtility();
                    var placeholders = new Hashtable();
                    placeholders.Add("[IPAddress]", userIpAddress);
                    placeholders.Add("[Password]", provisionalPassword);

                    var mailTemplate = this.ModelProvider.GetModel<MailTemplate>();
                    mailTemplate.Initialize(emailSubject, emailBody, emailAddress, placeholders);

                    var wasEmailSent = mailUtil.SendMail(mailTemplate);

                    if (wasEmailSent)
                    {
                        emailSent = true;
                    }
                    else
                    {
                        // var message = StorefrontManager.GetSystemMessage(StorefrontConstants.SystemMessages.CouldNotSentEmailError);
                        // result.SystemMessages.Add(new SystemMessage { Message = message });
                    }
                }
            }
            catch (Exception e)
            {
                result = new UpdatePasswordResult { Success = false };
                result.SystemMessages.Add(new SystemMessage { Message = e.Message });
            }

            return new ManagerResponse<UpdatePasswordResult, bool>(result, emailSent);
        }

        /// <summary>
        /// Changes the user password.
        /// </summary>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="currentPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>
        /// Result of the change password attempt
        /// </returns>
        public virtual ManagerResponse<UpdatePasswordResult, bool> ChangeUserPassword([NotNull] IVisitorContext visitorContext, string currentPassword, string newPassword)
        {
            Assert.ArgumentNotNull(visitorContext, nameof(visitorContext));
            Assert.ArgumentNotNullOrEmpty(currentPassword, nameof(currentPassword));
            Assert.ArgumentNotNullOrEmpty(newPassword, nameof(newPassword));

            var userName = visitorContext.UserName;
            var request = new UpdatePasswordRequest(userName, currentPassword, newPassword);
            var result = this.CustomerServiceProvider.UpdatePassword(request);

            if (!result.Success && !result.SystemMessages.Any())
            {
                var message = this.StorefrontContext.GetSystemMessage(AccountConstants.SystemMessages.ChangePasswordError);
                result.SystemMessages.Add(new SystemMessage { Message = message });
            }

            if (!result.Success)
            {
                Helpers.LogSystemMessages(result.SystemMessages, result);
            }

            return new ManagerResponse<UpdatePasswordResult, bool>(result, result.Success);
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
        public virtual ManagerResponse<AddPartiesResult, bool> AddAddress([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext, PartyEntity address)
        {
            throw new NotImplementedException();
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
        public virtual ManagerResponse<CustomerResult, bool> UpdateAddress([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext, PartyEntity address)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the current customer addresses.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="visitorContext">The visitor context.</param>
        /// <returns>
        /// The manager response with the customer addresses.
        /// </returns>
        public virtual ManagerResponse<GetPartiesResult, IEnumerable<PartyEntity>> GetCurrentCustomerAddresses([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext)
        {
            throw new NotImplementedException();
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
        public virtual ManagerResponse<CustomerResult, bool> DeleteAddress([NotNull] CommerceStorefront storefront, [NotNull] IVisitorContext visitorContext, string addressId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the parties.
        /// </summary>
        /// <param name="storefront">The storefront.</param>
        /// <param name="user">The user.</param>
        /// <param name="parties">The parties.</param>
        /// <returns>
        /// The manager result where the success flag is returned as the Result.
        /// </returns>
        public virtual ManagerResponse<CustomerResult, bool> RemoveParties([NotNull] CommerceStorefront storefront, [NotNull] CommerceCustomer user, List<Party> parties)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Updates the user.
        /// </summary>
        /// <param name="visitorContext">The visitor context.</param>
        /// <param name="firstName">The first name.</param>
        /// <param name="lastName">The last name.</param>
        /// <param name="phoneNumber">The phone number.</param>
        /// <param name="emailAddress">The email address.</param>
        /// <returns>
        /// The manager result where the success flag is returned as the Result.
        /// </returns>
        public virtual ManagerResponse<UpdateUserResult, CommerceUser> UpdateUser([NotNull] IVisitorContext visitorContext, string firstName, string lastName, string phoneNumber, string emailAddress)
        {
            Assert.ArgumentNotNull(visitorContext, nameof(visitorContext));
            Assert.ArgumentNotNullOrEmpty(emailAddress, nameof(emailAddress));

            var userName = visitorContext.UserName;
            var result = new UpdateUserResult { Success = false };

            var getUserResponse = this.GetUser(userName);
            var commerceUser = getUserResponse.Result;

            if (commerceUser != null)
            {
                commerceUser.FirstName = firstName;
                commerceUser.LastName = lastName;
                commerceUser.Email = emailAddress;
                commerceUser.SetPropertyValue("Phone", phoneNumber);

                try
                {
                    var request = new UpdateUserRequest(commerceUser);
                    result = this.CustomerServiceProvider.UpdateUser(request);
                }
                catch (Exception ex)
                {
                    result = new UpdateUserResult { Success = false };
                    result.SystemMessages.Add(new SystemMessage() { Message = ex.Message + "/" + ex.StackTrace });
                }
            }
            else
            {
                result.Success = false;

                foreach (var systemMessage in getUserResponse.ServiceProviderResult.SystemMessages)
                {
                    result.SystemMessages.Add(systemMessage);
                }
            }

            Helpers.LogSystemMessages(result.SystemMessages, result);
            return new ManagerResponse<UpdateUserResult, CommerceUser>(result, result.CommerceUser);
        }

        /// <summary>
        /// Errors the code to string.
        /// </summary>
        /// <param name="storefrontContext">Store front context.</param>
        /// <param name="createStatus">The create status.</param>
        /// <returns>The membership error status.</returns>
        protected virtual string ErrorCodeToString(IStorefrontContext storefrontContext, MembershipCreateStatus createStatus)
        {
            // See http://go.microsoft.com/fwlink/?LinkID=177550 for
            // a full list of status codes.
            string messageKey = AccountConstants.SystemMessages.UnknownMembershipProviderError;

            switch (createStatus)
            {
                case MembershipCreateStatus.DuplicateUserName:
                    messageKey = AccountConstants.SystemMessages.UserAlreadyExists;
                    break;
                case MembershipCreateStatus.DuplicateEmail:
                    messageKey = AccountConstants.SystemMessages.UserNameForEmailExists;
                    break;

                case MembershipCreateStatus.InvalidPassword:
                    messageKey = AccountConstants.SystemMessages.InvalidPasswordError;
                    break;

                case MembershipCreateStatus.InvalidEmail:
                    messageKey = AccountConstants.SystemMessages.InvalidEmailError;
                    break;

                case MembershipCreateStatus.InvalidAnswer:
                    messageKey = AccountConstants.SystemMessages.PasswordRetrievalAnswerInvalid;
                    break;

                case MembershipCreateStatus.InvalidQuestion:
                    messageKey = AccountConstants.SystemMessages.PasswordRetrievalQuestionInvalid;
                    break;

                case MembershipCreateStatus.InvalidUserName:
                    messageKey = AccountConstants.SystemMessages.UserNameInvalid;
                    break;

                case MembershipCreateStatus.ProviderError:
                    messageKey = AccountConstants.SystemMessages.AuthenticationProviderError;
                    break;

                case MembershipCreateStatus.UserRejected:
                    messageKey = AccountConstants.SystemMessages.UserRejectedError;
                    break;
            }

            return storefrontContext.GetSystemMessage(messageKey);
        }
    }
}
