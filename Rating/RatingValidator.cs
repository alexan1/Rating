using System;
using System.Collections.Generic;
using Rating.Model;

namespace Rating
{
    public static class RatingValidator
    {
        /// <summary>
        /// Validates a Rating object according to business rules.
        /// Returns a tuple of (isValid, errorMessage).
        /// </summary>
        public static (bool isValid, string errorMessage) Validate(Rating rating)
        {
            if (rating == null)
            {
                return (false, "Rating object cannot be null");
            }

            if (string.IsNullOrWhiteSpace(rating.UserId))
            {
                return (false, "UserId cannot be empty or whitespace");
            }

            if (rating.PersonId <= 0)
            {
                return (false, "PersonId must be greater than 0");
            }

            // Rate can be 0 (deletion), but otherwise must be 1-10
            if (rating.Rate != 0 && (rating.Rate < 1 || rating.Rate > 10))
            {
                return (false, "Rate must be 0 (to delete) or between 1 and 10");
            }

            return (true, null);
        }
    }
}
