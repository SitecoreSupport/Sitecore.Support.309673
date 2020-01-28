// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContactFactory.cs" company="Sitecore Corporation">
//   Copyright (c) Sitecore Corporation 1999-2016
// </copyright>
// <summary>
//   Defines the ContactFactory type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Sitecore.Support.Commerce.Contacts
{
    using Sitecore.Analytics;
    using Sitecore.Analytics.Tracking;
    using System.Web;
    using System.Linq;
    using Sitecore.Commerce.Providers;
    using Sitecore.Configuration;

    // DOCDONE

    /// <summary>
    /// Defines the current visitor using Sitecore Analytics.
    /// This class is needed to distinguish anonymous users from each other.
    /// </summary>
    public class ContactFactory
    {
        /// <summary>
        /// Gets the UserId of the current contact.
        /// </summary>
        /// <param name="contact">The current contact context.</param>
        /// <returns>The UserId of the current contact.</returns>
        [NotNull]
        public virtual string GetUserId(Contact contact)
        {
            if (contact == null)
            {
                return this.GetContact();
            }

            string user = null;
            if (Sitecore.Context.User.IsAuthenticated && contact.Identifiers.Count > 0)
            {
                // Try to find the commerce related identifier
                var commerceIdentifier = contact.Identifiers.Where(c => c.Source != null && c.Source.Equals(Sitecore.Commerce.Constants.ContactSource, System.StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (commerceIdentifier != null)
                {
                    user = commerceIdentifier.Identifier;
                }
                else
                {
                    var domainProvider = Factory.CreateObject("domainProvider", true) as IDomainProvider;
                    var domain = domainProvider.GetUserDomain();

                    commerceIdentifier = contact.Identifiers.Where(c => c.Identifier != null && c.Identifier.Contains(domain)).FirstOrDefault();
                    if (commerceIdentifier != null)
                    {
                        user = commerceIdentifier.Identifier;
                    }
                }
            }

            if (string.IsNullOrEmpty(user))
            {
                if (Sitecore.Context.User.IsAuthenticated && !Sitecore.Context.User.IsAdministrator)
                {
                    user = Sitecore.Context.User.Name;
                }
                else
                {
                    user = Sitecore.Data.ID.Parse(contact.ContactId).ToString();
                }
            }

            return user;
        }

        /// <summary>
        /// Gets the UserId of the current contact.
        /// </summary>
        /// <returns>The UserId of the current contact.</returns>
        [NotNull]
        public virtual string GetUserId()
        {
            return this.GetContact();
        }

        /// <summary>
        /// Gets the contact.
        /// </summary>
        /// <returns>The contact.</returns>
        [NotNull]
        public virtual string GetContact()
        {
            if (Tracker.Current == null)
            {
                if (HttpContext.Current != null &&
                    HttpContext.Current.User != null)
                {
                    return HttpContext.Current.User.Identity.Name;
                }

                return System.Threading.Thread.CurrentPrincipal.Identity.Name;
            }

            return this.GetUserId(Tracker.Current.Contact);
        }
    }
}