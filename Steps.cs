using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml;

namespace Steps
{
   /// <summary>
   /// Represents a range of values that some property can take on (ie temperature, voltage, etc)
   /// A Steps' values will be applied during tests.
   /// 
   /// TODO: The value semantics for Steps are inconsistent with with 
   ///      floating point arithmetic in general.  Usually NaN is not equal to anything, 
   ///      but semantically with are treating NaN as equal to anything.
   /// 
   ///  "The values represented are the following:
   ///   {x: ((Increment &gt;= 0)∧(FromValue &lt;= x &lt;= ToValue)) V
   ///      ((Increment &lt; 0)∧(ToValue &lt;= x &lt;= FromValue)) ∧ 
   ///   ((Increment = NaN) V (~(Increment = 0) ∧ 
   ///      (∃ y ∈ Z | x = FromValue + y*Increment ))}"
   ///   
   /// </summary>
   public sealed class Steps : IHWConfigValues
   {
      public event PropertyChangedEventHandler PropertyChanged;

      private static Regex StepsRegex = null;

      #region ### Steps Data ###

      private double _fromValue;
      private double _toValue;
      private double _increment;

      #endregion ### Steps Data ###

      bool _increasing = true;
      bool _incZero = false;

      private double _lastFromValue;
      private double _lastToValue;
      private double _lastIncrement;
      private bool _editing = false;

      [NonSerialized]
      private bool _fromServer;

      [NonSerialized]
      private bool _selected = false;
      

      /// <summary>
      /// Create a Steps with FromValue, ToValue, and Increment == 0, not from server
      /// </summary>
      public Steps() {
         _fromServer = false;
      }

      internal Steps(double fromValue, double toValue, double increment, bool fromServer)  {
         if (!(toValue < fromValue ^ increment > 0) && (toValue != fromValue && increment != 0))
            throw new System.ArgumentException("toValue cannot be less than fromValue.", "toValue");
         _fromValue = fromValue;
         _toValue = toValue;
         Increment = increment;
         _fromServer = fromServer;
      }

      /// <summary>
      /// Get a Steps from its string representation
      /// 
      /// TODO: the fromServer property is broken here.
      ///         It is possible for a non server steps to be deserialized as fromServer.
      ///         It just has to have a '*'.
      /// </summary>
      /// <param name="stepsIn">A string representing a steps to parse</param>
      /// <param name="stepsOut">If successful, a steps object parsed from stepsIn</param>
      /// <returns>true for success, false for failure</returns>
      internal Steps(string stepsIn) {
         if (!String.IsNullOrEmpty(stepsIn)) {
            // Parse anything similar to out output above.  Completely accepting on any whitespace variation,
            // Except that the string must being with a '*' to be templated
            if (StepsRegex == null) {
               StepsRegex = new System.Text.RegularExpressions.Regex(
                  @"^(\*?)\s*[Ff][rR]?[oO]?[mM]?\s*?(\S*)\s*?[Tt][oO]?\s*?(\S*)\s*[Bb][yY]?\s*(\S*)",
                  System.Text.RegularExpressions.RegexOptions.Compiled
                  );
            }

            bool server = false;
            double from;
            double to;
            double by;

            System.Text.RegularExpressions.Match match;

            if ((match = StepsRegex.Match(stepsIn)).Success) {
               server = match.Groups[1].Length == 1;
               if (Double.TryParse(match.Groups[2].Value, out from))
                  if (Double.TryParse(match.Groups[3].Value, out to))
                     if (Double.TryParse(match.Groups[4].Value, out by)) {
                        // Compare signs
                        if ((to < from ^ by > 0) && (to != from || by != 0))
                           throw new System.ArgumentException("toValue cannot be less than fromValue.");
                        _fromValue = from;
                        _toValue = to;
                        Increment = by;
                        _fromServer = server;
                        return;
                     }
            }
         }

         // stepsIn was not formatted correctly
         throw new ArgumentException("stepsIn was not formatted correctly", "stepsIn");
      }

      /// <summary>
      /// True if the Steps came from the server, false otherwise
      /// 
      /// </summary>
      public bool FromServer {
         get {
            return _fromServer;
         }
      }

      /// <summary>
      /// Inverse used for binding
      /// </summary>
      public bool NotFromServer {
         get {
            return !_fromServer;
         }
      }

      /// <summary>
      /// Determines whether the Steps' values will be applied during tests
      /// 
      /// </summary>
      public bool Selected {
         get {
            return _selected;
         }
         set {
            SetProperty(ref _selected, value, "Selected");
         }
      }

      /// <summary>
      /// The first value that this Steps attains
      /// 
      /// </summary>
      public double FromValue {
         get {
            return _fromValue;
         }
         set {
            SetServerProperty(ref _fromValue, value, "FromValue");
         }
      }

      /// <summary>
      /// The last value that this Steps can attain
      /// 
      /// </summary>
      public double ToValue {
         get {
            return _toValue;
         }
         set {
            SetServerProperty(ref _toValue, value, "ToValue");
         }
      }

      /// <summary>
      /// The increment to step by.
      /// A negative increment means to step backward.
      /// 
      /// Cannot be negative.
      /// An Increment of 0 means that the steps is not active,
      ///   and will not be applied during tests
      /// </summary>
      public double Increment {
         get {
            return _increment;
         }
         set {
            if (Double.IsInfinity(value))
               throw new ArgumentException("Increment cannot be infinite.");

            SetServerProperty(ref _increment, value, "Increment");
            _increasing = value > 0;
            _incZero = (value == 0);
         }
      }

      /// <summary>
      /// First value of the Steps or null if the Steps is empty
      /// </summary>
      public double? First {
         get {
            return IsEmpty ? null : (double?)_fromValue;
         }
      }

      public bool IsEmpty {
         get {
            return _incZero;
         }
      }

      #region ### IDataErrorInfo Implementation ###

      /// <summary>
      /// Returns a string indicating what is wrong with this Steps.
      /// </summary>
      public string Error {
         get {
            var errMessage = new System.Text.StringBuilder("");
            string line;

            if (_toValue < _fromValue && _increasing)
               errMessage.AppendLine("toValue cannot be less than fromValue.");

            if (!String.IsNullOrEmpty(line = this["Increment"]))
               errMessage.AppendLine(line);

            return errMessage.ToString();
         }
      }

      /// <summary>
      /// Return a string indicating what is wrong with the specified property
      /// </summary>
      /// <param name="columnName">Name of the property to check</param>
      /// <returns>An error message, or "" for no error.</returns>
      public string this[string columnName] {
         get {
            string res = String.Empty;
            switch (columnName) {
               case "FromValue":
                  if (_fromValue > _toValue)
                     res = "FromValue cannot be greater than ToValue.";
                  break;
               case "ToValue":
                  if(_toValue < _fromValue)
                     res = "ToValues cannot be less than FromValue.";
                  break;
               case "Increment":
                  if (Double.IsInfinity(_increment))
                     res = "Increment must be finite.";
                  break;
               default:
                  break;
            }
            return res;
         }
      }

      #endregion ### IDataErrorInfo Implementation ###

      internal void ToXml(XmlWriter writer) {
         writer.WriteStartElement(typeof(Steps).Name);
         writer.WriteAttributeString("FromValue", _fromValue.ToString());
         writer.WriteAttributeString("ToValue", _toValue.ToString());
         writer.WriteAttributeString("Increment", _increment.ToString());
         writer.WriteAttributeString("Selected", _selected.ToString());
         writer.WriteEndElement();
      }

      internal static Steps FromXml(XmlReader reader, bool fromServer) {
         double fromValue = 0;
         double toValue = 0;
         double increment = 0;
         bool selected = false;

         reader.MoveToContent();

         if (reader.MoveToAttribute("FromValue") && reader.ReadAttributeValue()) {
            if (!Double.TryParse(reader.Value, out fromValue)) {
               reader.ThrowError(ThrowHelper.XmlErrorTypes.BadData);
            }
         } else {
            reader.ThrowError(ThrowHelper.XmlErrorTypes.MissingAttribute, "FromValue");
         }

         reader.MoveToElement();

         if (reader.MoveToAttribute("ToValue") && reader.ReadAttributeValue()) {
            if (!Double.TryParse(reader.Value, out toValue)) {
               reader.ThrowError(ThrowHelper.XmlErrorTypes.BadData);
            }
         } else {
            reader.ThrowError(ThrowHelper.XmlErrorTypes.MissingAttribute, "ToValue");
         }

         reader.MoveToElement();

         if (reader.MoveToAttribute("Increment") && reader.ReadAttributeValue()) {
            if (!Double.TryParse(reader.Value, out increment)) {
               reader.ThrowError(ThrowHelper.XmlErrorTypes.BadData);
            }
         } else {
            reader.ThrowError(ThrowHelper.XmlErrorTypes.MissingAttribute, "Increment");
         }

         reader.MoveToElement();

         if (reader.MoveToAttribute("Selected") && reader.ReadAttributeValue()) {
            if (!Boolean.TryParse(reader.Value, out selected)) {
               reader.ThrowError(ThrowHelper.XmlErrorTypes.BadData);
            }
         } else {
            reader.ThrowError(ThrowHelper.XmlErrorTypes.MissingAttribute, "Selected");
         }

         reader.MoveToElement();
         reader.ReadStartElement();

         // Never reached...
         return new Steps(fromValue, toValue, increment, fromServer);
      }

      /// <summary>
      /// Determine if this Steps has value p
      /// 
      /// Runs in constant time: O(1)
      /// 
      /// As from above:
      ///  "The values represented are the following:
      ///   {x: ((Increment &gt;= 0)∧(FromValue &lt;= x &lt;= ToValue)) V
      ///      ((Increment &lt; 0)∧(ToValue &lt;= x &lt;= FromValue)) ∧ 
      ///   ((Increment = NaN) V (~(Increment = 0) ∧ 
      ///      (∃ y ∈ Z | x = FromValue + y*Increment ))}"
      /// </summary>
      /// <param name="p"></param>
      /// <returns></returns>
      public bool HasValue(double x) {
         // (FromValue <= x <= ToValue)
         if (
            // (Increment >= 0) ∧ (FromValue <= x <= ToValue)
            ((_increasing || _incZero) && _fromValue <= x && x <= _toValue) ||
            // (Increment < 0) ∧ (ToValue <= x <= FromValue)
            (_toValue <= x && x <= _fromValue)) {
            // And
            // (Increment = NaN)
            if (Double.IsNaN(_increment)) 
               return true;
            // Or
            // ~(Increment = 0)
            else if(_increment != 0) {
               // And
               // (∃ y ∈ Z | x = FromValue + y*Increment )
               double y = (x - _fromValue)/_increment;

               // y ∈ Z?
               if (((int)y) == y)
                  return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Determines if this Steps is a subset of super
      /// 
      /// Runs in constant time: O(1)
      /// 
      /// Specifically:
      ///   Steps a ⊂ Steps b iff ∀x ∈ R, a.HasValue(x) implies b.HasValue(x) 
      /// 
      /// </summary>
      /// <param name="super">Steps which may be a supeset of this one</param>
      /// <returns>true if this Steps is a subset of super, false otherwise</returns>
      public bool IsSubsetOf(Steps super) {
         // Empty set is a subset of everything
         if (_incZero)
            return true;

         // Treat a null super as an empty set
         //    and
         // Eliminate the obvious cases
         if (super != null && super.HasValue(_fromValue)) {
            // There are now two ways this can be a subset of super
            // this could be a single value
            if (Double.IsNaN(_increment) || _increment > Math.Abs(_fromValue - _toValue))
               return true;
            // Or
            //
            // this does not extend outside super
            double boundry = _toValue - _increment;
            if (((boundry < super._toValue ^ boundry < super._fromValue) &&
                  (boundry != super._toValue || boundry != super._fromValue))) {
               // And
               // this._increment can be an integer multiple of super._increment
               // NOTE: The modulus operator does not really work with doubles
               //       So convert to decimal for precision.
               decimal thisIncDec = Convert.ToDecimal(_increment);
               decimal supIncDec = Convert.ToDecimal(super._increment);
               if (Double.IsNaN(super._increment) || (thisIncDec % supIncDec) == 0)
                  return true;
            }
         }
         return false;
      }

      #region ### IEditableObject Implementation ###

      /// <summary>
      /// Begin editing the Steps
      /// </summary>
      public void BeginEdit() {
         _editing = true;
         _lastFromValue = _fromValue;
         _lastToValue = _toValue;
         _lastIncrement = _increment;
      }

      /// <summary>
      /// Cancel the current edit.
      /// </summary>
      public void CancelEdit() {
         if (_editing) {
            _fromValue = _lastFromValue;
            _toValue = _lastToValue;
            _increment = _lastIncrement;
         }
      }

      /// <summary>
      /// End the current edit.
      /// Note that this method is invoked whether or not the edit was cancelled
      /// </summary>
      public void EndEdit() {
         _editing = false;
         _notifyAll();
      }

      #endregion ### IEditableObject Implementation ###

      #region ### ICloneable Implementation ###

      public object Clone() {
         return new Steps(_fromValue, _toValue, _increment, _fromServer) { _selected = this._selected };
      }

      #endregion ### ICloneable Implementation ###

      private void _notifyAll() {
         if (PropertyChanged != null) {
            PropertyChanged(this, new PropertyChangedEventArgs("FromValue"));
            PropertyChanged(this, new PropertyChangedEventArgs("ToValue"));
            PropertyChanged(this, new PropertyChangedEventArgs("Increment"));
            PropertyChanged(this, new PropertyChangedEventArgs("Selected"));
         }
      }

      /// <summary>
      /// Yields all Values that this Steps represents.
      /// 
      /// If _increment is positive, values are returned increasing.
      /// If _increment is negative, values are returned in decreasing order.
      /// </summary>
      /// <returns>An IEnumerable which can be used to iterate over this Steps' values.</returns>
      public IEnumerable<double> Values() {
         // Increment must be positive (nonzero)
         double d = _fromValue;

         // We have to recalculate the value at each iteration.
         // Otherwise even mild drift will ruin everything.
         //
         // For instance with 8-byte double precision arithmetic,
         //    0.75 +.2+.2+.2 != 1.35
         if (_increment > 0) {
            while (d <= _toValue) {
               yield return d;
               d += _increment;
            }
         } else if (_increment < 0) {
            while (d >= _toValue) {
               yield return d;
               d += _increment;
            }
         }
      }

      private void SetServerProperty<K>(ref K field, K value, string name) {
         if (!_fromServer)
            SetProperty(ref field, value, name);
         else
            throw new InvalidOperationException("Cannot change steps that came from the server.");
      }

      private void SetProperty<K>(ref K field, K value, string name) {
         if (!EqualityComparer<K>.Default.Equals(field, value)) {
            field = value;
            if (PropertyChanged != null) {
               PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
         }
      }

      #region ### Public Overrides ###

      /////// <summary>
      /////// Determines if this Steps is equal to obj
      /////// 
      /////// Two steps are equal if they have the same:
      ///////   _toValue, _fromValue, _increment, _enabled
      /////// </summary>
      /////// <param name="obj"></param>
      /////// <returns></returns>
      ////public override bool Equals(object obj) {
      ////   var objSteps = obj as Steps;
      ////   if (objSteps != null)
      ////      return _toValue == objSteps._toValue &&
      ////         _fromValue == objSteps._fromValue &&
      ////         _increment == objSteps._increment;
      ////   else
      ////      return false;
      ////}

      /////// <summary>
      /////// TODO: figure out what the best way to do this is.  See Words of Advice.txt
      /////// </summary>
      /////// <returns></returns>
      ////public override int GetHashCode() {
      ////   return _toValue.GetHashCode() * 37 ^
      ////         _fromValue.GetHashCode() * 31 ^
      ////         _increment.GetHashCode();
      ////}

      /// <summary>
      /// Writes the Steps as a readable string
      /// If the Steps is a templated steps, we prepend with a '*' character
      /// 
      /// Examples:
      ///   "From 0 To 1.5 By 0.1"
      ///   "*From -Infinity to Infinity By 0"
      /// </summary>
      /// <returns></returns>
      public override string ToString() {
         return (_fromServer ? "*" : "") + "From " + _fromValue + " To " + _toValue + " By " + _increment;
      }

      #endregion ### Public Overrides ###

   }
}
