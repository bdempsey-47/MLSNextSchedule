Absolutely. Below is a \*\*clean, implementation-ready Technical Requirements Document (TRD)\*\* you can hand directly to an AI agent or use yourself as the authoritative reference.



I’ve kept it \*\*explicit, unambiguous, and tool-agnostic\*\*, while reflecting everything you’ve discovered through real testing.



---



\# Technical Requirements Document



\## MLS NEXT Schedule Ingestion \& Custom Web UI



\### Project Goal



Create a custom, mobile-friendly web application that ingests youth soccer match schedules from the \*\*MLS Next\*\* platform, stores the data in a cloud database, and exposes it via a custom Web API and UI optimized for usability.



---



\## 1. Source System Overview



\### Data Origin



\* Frontend site: MLS NEXT schedule pages

\* Backend data provider: \*\*Modular11\*\*

\* Data access method: undocumented Web API returning \*\*HTML fragments\*\* (not JSON)

\* Authentication: \*\*none required\*\*

\* Rate limiting: none observed (polite throttling recommended)



---



\## 2. API Endpoint Specification



\### Endpoint URL



```

GET https://www.modular11.com/public\_schedule/league/get\_matches

```



\### Request Type



\* HTTP GET

\* XHR-style request

\* Returns \*\*HTML partials\*\* containing both desktop and mobile markup



---



\## 3. Required Query Parameters



| Parameter    | Description                                   | Example               |

| ------------ | --------------------------------------------- | --------------------- |

| `tournament` | League identifier (MLS NEXT Academy Division) | `35`                  |

| `gender`     | Gender filter (1 = Male)                      | `1`                   |

| `age\[]`      | Age group (repeatable)                        | `15` (U15)            |

| `status`     | Match status                                  | `scheduled`           |

| `match\_type` | Competition match type                        | `2`                   |

| `start\_date` | Start of date window                          | `YYYY-MM-DD HH:mm:ss` |

| `end\_date`   | End of date window                            | `YYYY-MM-DD HH:mm:ss` |

| `open\_page`  | Pagination control                            | integer               |



\### Optional / Pass-Through Parameters



These may be included but are not required for ingestion:



\* `academy`

\* `team`

\* `location`

\* `groups`

\* `brackets`

\* `match\_number`



---



\## 4. Pagination Behavior (Critical)



\### Observed Behavior



\* `open\_page = 0` → returns \*\*page 1\*\*

\* `open\_page = 1` → returns \*\*page 1 (duplicate)\*\*

\* `open\_page >= 2` → subsequent pages

\* Out-of-bounds page returns:



&nbsp; ```

&nbsp; <h2 class="text-center block-without-matches">No data available.</h2>

&nbsp; ```



\### Pagination Strategy (Required)



\* Start pagination at `open\_page = 1`

\* Increment `open\_page` by +1 per request

\* Continue until response body contains:



&nbsp; ```

&nbsp; No data available

&nbsp; ```



\### Pagination Stop Conditions



The ingestion loop \*\*must stop\*\* when:



\* The response contains the string `No data available`

\* OR no valid Match IDs are parsed from the response



---



\## 5. Response Format



\### Content Type



\* `text/html`

\* Partial HTML fragments intended for DOM injection



\### Structure Characteristics



\* Each match appears \*\*twice\*\*:



&nbsp; \* Desktop markup

&nbsp; \* Mobile markup

\* Both contain identical data

\* Visibility controlled via CSS (`visible-xs`, `hidden-xs`, etc.)



---



\## 6. Parsing Strategy (Mandatory)



\### Layout Selection



\* \*\*Parse ONLY the mobile version\*\*

\* Anchor on containers with class:



&nbsp; ```

&nbsp; visible-xs

&nbsp; ```



\### Match Identity (Primary Key)



Each match includes a unique \*\*Match ID\*\*, rendered as:



```

Label: "Match ID"

Value: numeric string (e.g. 83984)

```



This Match ID:



\* Is globally unique

\* Is stable across pages

\* Must be used as the \*\*natural key\*\*



---



\## 7. Required Fields to Extract (Mobile Markup)



| Field              | Source                             |

| ------------------ | ---------------------------------- |

| Match ID           | “Match ID” value                   |

| Match Date/Time    | “Date” value                       |

| Home Team Name     | “Home Team”                        |

| Away Team Name     | “Away Team”                        |

| Age Group          | “Age” (e.g. U13)                   |

| Gender             | “Gender”                           |

| Competition        | “Competition” (e.g. AD)            |

| Division           | “Division”                         |

| Venue / Field Name | Second line of date/location block |

| Score              | Score field (often `TBD`)          |



\### Ignored Elements



\* Images

\* CSS classes

\* Desktop layout

\* Visual-only markup



---



\## 8. Deduplication Rules



\### Ingestion-Level Deduplication



\* Maintain an in-memory set of Match IDs per run

\* Skip parsing if Match ID already seen



\### Database-Level Deduplication



\* Enforce UNIQUE constraint on Match ID

\* Use \*\*UPSERT\*\* semantics on insert



---



\## 9. Data Storage Requirements



\### Primary Data Store



\* Relational database (PostgreSQL, Azure SQL, etc.)

\* Normalized OLTP schema (NOT star schema)



\### Core Tables (Suggested)



\* Match

\* Team

\* Venue

\* Division

\* Competition

\* AgeGroup



\### Raw Data Retention (Recommended)



\* Store raw HTML responses temporarily (blob or text)

\* Purpose:



&nbsp; \* Debugging

&nbsp; \* Reprocessing

&nbsp; \* Parser evolution

\* Retention policy optional (e.g. delete after validation)



---



\## 10. Ingestion Job Requirements



\### Execution Model



\* Background job / scheduled task

\* Safe to re-run (idempotent)



\### Behavior



1\. Initialize `open\_page = 1`

2\. Call API

3\. If response contains `No data available` → stop

4\. Parse mobile blocks

5\. Extract unique Match IDs

6\. Normalize and upsert records

7\. Increment `open\_page`

8\. Repeat



\### Operational Safeguards



\* Throttle requests (100–300 ms)

\* Log:



&nbsp; \* page number

&nbsp; \* match count

&nbsp; \* execution time

\* Graceful failure with resumability



---



\## 11. API Layer (Your System)



Your custom backend API should:



\* Serve match data from your database

\* Support filtering by:



&nbsp; \* Team

&nbsp; \* Date range

&nbsp; \* Age group

&nbsp; \* Division

\* Be decoupled from Modular11 availability



---



\## 12. Analytics \& Star Schema (Out of Scope for Phase 1)



\* Star schema \*\*not required\*\* for initial implementation

\* May be added later for dashboards / reporting

\* Core ingestion must remain normalized



---



\## 13. Non-Functional Requirements



\* No scraping of full pages

\* No headless browsers

\* No reliance on CSS visibility

\* No hardcoded page counts

\* Fully deterministic ingestion



---



\## 14. Known Constraints



\* API is undocumented and UI-oriented

\* Response format may change (HTML parsing required)

\* No SLA or stability guarantees from source



---



\## 15. Success Criteria



\* All scheduled matches ingested without duplicates

\* Matches retrievable without calling external API

\* Mobile-friendly UI superior to original site

\* Ingestion process repeatable and safe



---



If you want, next I can:



\* Convert this into a \*\*machine-optimized spec\*\* (YAML / JSON)

\* Write \*\*prompt instructions\*\* for the AI agent

\* Generate \*\*starter ingestion code\*\* in Python or C#



Just tell me what format your AI agent prefers.



