﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Web;

/******************************************************
 * 
 * Copyright (c) 2008-2016 MyFlightbook LLC
 * Contact myflightbook-at-gmail.com for more information
 *
*******************************************************/

namespace MyFlightbook.Histogram
{
    /// <summary>
    /// Represents the x-axis on a histogram
    /// </summary>
    [Serializable]
    public class Bucket<T> : IComparable where T : IComparable
    {
        #region Properties
        /// <summary>
        /// The name that is displayed for the bucket
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// For sorting purposes, the ordinal bucket value
        /// </summary>
        public IComparable Ordinal { get { return (IComparable)OrdinalValue; } }

        public T OrdinalValue { get; set; }

        /// <summary>
        /// The value (Y-axis) for this bucket.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// The running total (Y-axis) for this bucket
        /// </summary>
        public double RunningTotal { get; set; }
        #endregion

        #region Object creation
        public Bucket(T ordinalValue, string szName = "", double val = 0.0)
        {
            DisplayName = szName;
            OrdinalValue = ordinalValue;
            Value = val;
            RunningTotal = 0.0;
        }
        #endregion

        #region IComparable
        public int CompareTo(object obj)
        {
            return Ordinal.CompareTo(obj);
        }
        #endregion
    }

    #region Bucket Management
    /// <summary>
    /// Abstract base class for a bucket manager, which manages buckets for a data type.  Two concreate subclasses exist: a date manager and a range manager.
    /// The purpose of this class is to provide the buckets for the x-axis of the histogram.
    /// </summary>
    public abstract class BucketManager<T> where T : IComparable
    {
        public IEnumerable<Bucket<T>> Buckets { get; set; }

        /// <summary>
        /// Scans the set of items and returns a complete set of buckets.  The buckets should include at least all possible items, and optionally include 
        /// </summary>
        /// <param name="items">An enumerable of the items to be scanned</param>
        /// <returns>A keyed collection of buckets, using the Histogrammable's BucketKey as the key</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        protected abstract IDictionary<IComparable, Bucket<T>> BucketsForData(IEnumerable<IHistogramable> items);

        /// <summary>
        /// Returns the bucket key for the specified comparable.  E.g., for date-based, would return April 1, 2016 for a passed date of April 5, 2016.  Or for integers, might return "1" for passed value of 75 if bucket size is 100, or 2 if bucketize is 50.
        /// </summary>
        /// <param name="o">The comparable</param>
        /// <returns></returns>
        protected abstract IComparable KeyForValue(IComparable o);

        /// <summary>
        /// Scans the source data, building a set of buckets.  Makes two or three passes: once to determine the buckets, once to fill them, and optionally once to set running totals
        /// </summary>
        /// <param name="source">An enumerable list of Histogrammable objects</param>
        /// <param name="context">Any context to be passed (e.g., could specify the property to be summed for the histogram</param>
        /// <param name="fSetRunningTotals">True to set running totals</param>
        /// <returns>The resulting histogram buckets, which are also persisted in the "Buckets" property</returns>
        public void ScanData(IEnumerable<IHistogramable> source, object context = null, bool fSetRunningTotals = false)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            IDictionary<IComparable, Bucket<T>> dict = this.BucketsForData(source);

            foreach (IHistogramable h in source)
                dict[KeyForValue(h.BucketSelector)].Value += h.HistogramValue(context);

            Buckets = dict.Values;

            if (fSetRunningTotals)
            {
                double total = 0.0;
                foreach (Bucket<T> b in Buckets)
                    b.RunningTotal = (total += b.Value);
            }
        }

    }

    /// <summary>
    /// BucketManager for year-month (e.g., "2016-May") data
    /// </summary>
    public class YearMonthBucketmanager : BucketManager<DateTime>
    {
        #region properties
        /// <summary>
        /// True (default) to align the buckets to the January of the earliest year
        /// </summary>
        public bool AlignStartToJanuary { get; set; }

        /// <summary>
        /// True to align the buckets to December of the latest year.  False by default.
        /// </summary>
        public bool AlignEndToDecember { get; set; }

        /// <summary>
        /// Format string to use.  Default is "MMM yyyy"
        /// </summary>
        public string DateFormat { get; set; }
        #endregion

        public YearMonthBucketmanager() : base()
        {
            AlignStartToJanuary = true;
            AlignEndToDecember = false;
            DateFormat = "MMM yyyy";
        }

        /// <summary>
        /// Generates the buckets for the range of Histogrammable.  WILL THROW AN EXCEPTION IF THE ORDINAL IS NOT A DATETIME!
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        protected override IDictionary<IComparable, Bucket<DateTime>> BucketsForData(IEnumerable<IHistogramable> items)
        {
            DateTime _dtMin = DateTime.MaxValue, _dtMax = DateTime.MinValue;

            Dictionary<IComparable, Bucket<DateTime>> dict = new Dictionary<IComparable, Bucket<DateTime>>();
            if (items == null)
                throw new ArgumentNullException("items");

            if (items.Count() == 0)
                return dict;

            // Find the date range.
            foreach (IHistogramable h in items)
            {
                DateTime dt = (DateTime)h.BucketSelector;
                if (_dtMin.CompareTo(dt) > 0)
                    _dtMin = dt;
                if (_dtMax.CompareTo(dt) < 0)
                    _dtMax = dt;
            }

            // Align to 1st of the month, and optionallyi to January/December.
            _dtMin = new DateTime(_dtMin.Year, AlignStartToJanuary ? 1 : _dtMin.Month, 1);
            _dtMax = new DateTime(_dtMax.Year, AlignEndToDecember ? 12 : _dtMax.Month, 1);

            // Now create the buckets
            do {
                dict[_dtMin] = new Bucket<DateTime>((DateTime) KeyForValue(_dtMin), _dtMin.ToString(DateFormat, CultureInfo.CurrentCulture));
                _dtMin = _dtMin.AddMonths(1);
            } while (_dtMin.CompareTo(_dtMax) <= 0);

            return dict;
        }

        protected override IComparable KeyForValue(IComparable o)
        {
            DateTime dt = (DateTime)o;
            return new DateTime(dt.Year, dt.Month, 1);
        }
    }

    /// <summary>
    /// BucketManager for numeric ranges (e.g., "0, 1-200, 201-300, etc.)
    /// </summary>
    public class NumericBucketmanager : BucketManager<int>
    {
        #region properties
        /// <summary>
        /// Width of each bucket (e.g., 200 items, 100 items.  Default is 100
        /// </summary>
        public int BucketWidth { get; set; }

        /// <summary>
        /// True if 0 should get its own bucket (in which case next bucket begins with 1).  Default is false.
        /// </summary>
        public bool BucketForZero { get; set; }
        #endregion

        public NumericBucketmanager()
            : base()
        {
            BucketWidth = 100;
            BucketForZero = false;
        }

        /// <summary>
        /// Generates the buckets for the range of Histogrammable.  WILL THROW AN EXCEPTION IF THE ORDINAL IS NOT AN INTEGER!
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        protected override IDictionary<IComparable, Bucket<int>> BucketsForData(IEnumerable<IHistogramable> items)
        {
            Dictionary<IComparable, Bucket<int>> dict = new Dictionary<IComparable, Bucket<int>>();
            if (items == null)
                throw new ArgumentNullException("items");

            if (items.Count() == 0)
                return dict;

            if (BucketWidth == 0)
                throw new InvalidOperationException("Buckets cannot have 0 width!");

            int min = Int32.MaxValue, max = Int32.MinValue;

            // Find the date range.
            foreach (IHistogramable h in items)
            {
                int i = (int) h.BucketSelector;
                if (i < min)
                    min = i;
                if (i > max)
                    max = i;
            }

            if (min > 0 && BucketForZero)
                min = 0;

            // Align to bucket width
            min = (int) KeyForValue(min);

            // Now create the buckets
            do
            {
                if (min == 0 && BucketForZero)
                {
                    dict[KeyForValue(min)] = new Bucket<int>((int)KeyForValue(min), min.ToString(CultureInfo.CurrentCulture));      // zero bucket
                    ++min;
                    dict[KeyForValue(min)] = new Bucket<int>((int)KeyForValue(min), String.Format(CultureInfo.CurrentCulture, "{0}-{1}", min, BucketWidth - 1));   // 1-(Bucketwidth-1) bucket
                    min = BucketWidth;
                }
                else
                {
                    dict[KeyForValue(min)] = new Bucket<int>((int)KeyForValue(min), String.Format(CultureInfo.CurrentCulture, "{0}-{1}", min, min + BucketWidth - 1));
                    min += BucketWidth;
                }
            } while (min < max);

            return dict;
        }

        /// <summary>
        /// Returns the key for the specified integer, based on bucketwidth.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        protected override IComparable KeyForValue(IComparable o)
        {
            int i = (int)o;

            if (i == 0)
                return BucketForZero ? 0 : 1;

            return (int) ((i < 0 ? -1 : 1) * Math.Ceiling(Math.Abs(i) / (double)BucketWidth));
        }
    }
    #endregion


    /// <summary>
    /// Interface implemented by objects which can be summed up in a histogram
    /// </summary>
    public interface IHistogramable
    {
        /// <summary>
        /// Returns the IComparable that determines the bucket to which this gets assigned.
        /// </summary>
        IComparable BucketSelector { get; }

        /// <summary>
        /// Examines the object and returns the value that should be added to that bucket's total
        /// </summary>
        /// <param name="context">An optional parameter for context.  E.g., if there are multiple fields that can be summed, this could specify the field to use</param>
        /// <returns>The value to add to the bucket for this item</returns>
        double HistogramValue(object context = null);
    }
}
