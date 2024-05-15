# DTDLParserSample using Co-Types


## Co-Types

DTDL allows to add arbitrrary, undefined element types as co-types.  When an element has an co-type, undefined properties with arbitrary values can be added to the element.

Please refer to https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/DTDL.v2.md#undefined-co-types-and-properties for more details on co-types.

### Example

- Co-Types : MinMax
- Property = Value : maxValue = 10, minValue = 1

```json
{
    "@type": [
        "Property",
        "MinMax"
    ],
    "displayName": {
        "en": "Simple Property with MinMax Type"
    },
    "name": "simplePropertyWithMinMax",
    "schema": "integer",
    "maxValue": 10,
    "minValue": 1,
    "writable": true
}
```

## DTDL v3 Language Extension & Co-type

DTDL v3 supports language extensions (https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v3/DTDL.Extensions.md).  With DTDL v3, we can use co-types under Language Extension concept.

## How to extend DTDL with co-types.

1. (DTDL v3 only) Add DTDL ID for the extension's context specifier  
    &nbsp; 
    e.g. add **`dtmi:com:company_foo:extension:dummy;1`** to **`@context`**
    &nbsp;  
    ```json
    {
    "@context": [ "dtmi:dtdl:context;3", "dtmi:com:company_foo:extension:dummy;1"` ],
    "@id": "dtmi:com:company_foo:sample_base_model;1",
    "@type": "Interface",
        :
        :
    ```

1. Add a co-type(s) to elements, then define property and value
    &nbsp;
    Example 1: Simple Integer Property with co-type **`MinMax`**
    &nbsp;
    ```json
    {
        "@type": [
            "Property",
            "MinMax"
        ],
        "name": "PropertyWithMainMax",
        "schema": "integer",
        "maxValue": 10,
        "minValue": 1,
    }
    ```
    &nbsp;
    Example 2 : JSON Object type with co-typed fields
    &nbsp;
    ```json
    {
        "@type": "Property",
        "name": "ObjectWithMinMaxInField",
        "schema": {
            "@type": "Object",
            "fields": [
                {
                    "@type": [ "Field", "MinMax" ],
                    "name": "Field1_Integer",
                    "schema": "integer",
                    "minValue": 0,
                    "maxValue": 10
                },
                {
                    "@type": [ "Field", "MinMax" ],
                    "name": "Field2_Double",
                    "schema": "double",
                    "minValue": 0.1,
                    "maxValue": 9.9
                }
            ]
        }
    }
    ```

1. Parse DTDL

    >
    > [!IMPORTANT]  
    > For DTDL v3, ParsingOption **AllowUndefinedExtensions** must be set to Always
    >

    ```csharp
    using DTDLParser;  

        :
        :
    
    ModelParser parser = new ModelParser(
        new ParsingOptions()
        {
            AllowUndefinedExtensions = WhenToAllow.Always
        }
    );
    ```

1. Co-types and properties and values will be parsed to **`UndefinedTypes`** and **`UndefinedProperties`**

    ```text
    - DtEntity                  : {DTDLParser.Models.DTPropertyInfo}
      +	ChildOf	                : {dtmi:com:company_foo:sample_base_model;1}
      +	ClassId	                : {dtmi:dtdl:class:Property;3}
        Comment	                : null
      +	DefinedIn               : {dtmi:com:company_foo:sample_base_model;1}
      +	Description	            : Count = 0
      +	DisplayName	            : Count = 1
        EntityKind	            : Property
      +	Id	                    : {dtmi:com:company_foo:sample_base_model:_contents:__simplePropertyWithMinMax;1}
        LanguageMajorVersion    : 3
        Name	                : "simplePropertyWithMinMax"
      +	Schema	                : {DTDLParser.Models.DTIntegerInfo}
      +	SupplementalProperties  : Count = 0
      +	SupplementalTypes	    : Count = 0
      -	UndefinedProperties	    : Count = 2	
        - [0]	                : {[maxValue, ValueKind = Number : "10"]}	
            Key	                : "maxValue"
            Value	            : ValueKind = Number : "10"
        - [1]                   : {[minValue, ValueKind = Number : "1"]}
            Key	                : "minValue"
            Value	            : ValueKind = Number : "1"
      -	UndefinedTypes	        : Count = 1
        - [0]                   : "MinMax"
    ```

## Examples

`dtmi/com/company_foo` contains 2 JSON files.  

- [dtmi/com/company_foo/sample_base_model-1.json](./dtmi/com/company_foo/sample_base_model-1.json)  
  A base DTDL model.
  
- [dtmi/com/company_foo/sample_device-1.json](./dtmi/com/company_foo/sample_device-1.json)  
  An example of DTDL model for a device exnteded from base model above.

## Parser Sample

This sample application does followings.


1. Takes simulated property payload from JSON file.  
    Specify a JSON file with '-i' option.  
    &nbsp;  
    Example : 
    &nbsp;  
  
    ```json
    {
        "$modelId" : "dtmi:com:company_foo:sample_device;1",
        "objectWithMinMaxInField" : {
            "Field1_Integer" : 1000,
            "Field2_Double" : 9.5
        },
        "simplePropertyWithMinMax" : 12,
        "objectWithMinMax" : {
            "IntegerField" : 10,
            "DoubleField" : 1.2
        },
        "objectWithMinMaxArray" : {
            "IntegerField" : 12,
            "DoubleField" : 1.2
        },
        "device_states": {
            "power_states" : {
            "power_level" : 120
            }
        }
    }
    ```

1. Parse DTDL model files  
  For the purpose of the sample, it assumes the DTDL Json files are on local file system (vs. network/Github etc).
  Specify the folder containing DTDL files with `-d` option.  The application will resolve and parse DTDL files based on `$modelId` specified in `input file`.

1. Cross check input payload against Model Files for `MinMax` co-type, then values against `minValue` and `maxValue`.

## Example

```console
DTDLParserSample.exe -i C:\Work\Repo\DTDLParserSample\input\input.json -d C:\Work\Repo\DTDLParserSample\DTDL
```

```console

C:\Work\Repo\DTDLParserSample\DTDLParserSample\bin\Release\net8.0>DTDLParserSample.exe -i C:\Work\Repo\DTDLParserSample\input\input.json -d C:\Work\Repo\DTDLParserSample\DTDL
DTDL Parser (dtdl parser library version 1.0.52.41181
Reading simulated data : 'C:\Work\Repo\DTDLParserSample\input\input.json'
Found DTDL ID 'dtmi:com:company_foo:sample_device;1' in 'C:\Work\Repo\DTDLParserSample\input\input.json'
Parsing DTDL in 'C:\Work\Repo\DTDLParserSample\DTDL' for 'dtmi:com:company_foo:sample_device;1'
Reading C:\Work\Repo\DTDLParserSample\DTDL\dtmi\com\company_foo\sample_device-1.json
Parsing DTDL in C:\Work\Repo\DTDLParserSample\DTDL for dtmi:com:company_foo:sample_base_model;1
Reading C:\Work\Repo\DTDLParserSample\DTDL\dtmi\com\company_foo\sample_base_model-1.json
=================================================
  Interface Type : Property
    Display Name : Object with MinMax Type in field
     Description : <No Description>
            Name : objectWithMinMaxInField
          Schema : DTDLParser.Models.DTObjectInfo
        ----------------
                   Field : Name = Field1_Integer
                         : Display Name = <No Display Name>
                         : Description = <No Description>
                         : Schema = Integer
     CoType Type : integer
     CoType Type : MinMax
 CoType Property : minValue = 0
 CoType Property : maxValue = 10
        ----------------
                   Field : Name = Field2_Double
                         : Display Name = <No Display Name>
                         : Description = <No Description>
                         : Schema = Double
     CoType Type : double
     CoType Type : MinMax
 CoType Property : minValue = 0.1
 CoType Property : maxValue = 9.9
=================================================

   :
   :

=================================================
      Input Data
        Property
----------------
            Name : objectWithMinMaxInField
           Field : Name  = Field1_Integer
                 : Value = 1000
                 : Invalid : Value 1000 outside of min 0 - max 10 range
           Field : Name  = Field2_Double
                 : Value = 9.5
                 : Valid : Value 9.5 within min 0.1 - max 9.9 range
----------------
            Name : simplePropertyWithMinMax
        Property : Name  = simplePropertyWithMinMax
                 : Value = 12
                 : Invalid : Value 12 outside of min 1 - max 10 range
----------------
            Name : objectWithMinMax
           Field : Name  = IntegerField
                 : Value = 10
                 : Valid : Value 10 within min 0.1 - max 10 range
           Field : Name  = DoubleField
                 : Value = 1.2
                 : Valid : Value 1.2 within min 0.1 - max 10 range
----------------
            Name : objectWithMinMaxArray
           Field : Name  = IntegerField
                 : Value = 12
                 : Invalid : Value 12 outside of min 1 - max 10 range
           Field : Name  = DoubleField
                 : Value = 1.2
                 : Valid : Value 1.2 within min 0.1 - max 9.9 range
----------------
            Name : device_states
           Field : Name  = power_states
           Field : Name  = power_level
                 : Value = 120
                 : Invalid : Value 120 outside of min 0 - max 100 range
```