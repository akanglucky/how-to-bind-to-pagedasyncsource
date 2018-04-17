﻿using DevExpress.Data.Filtering;
using DevExpress.Xpf.Data;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PagedAsyncSourceSample {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            var source = new PagedAsyncSource() {
                ElementType = typeof(IssueData),
                PageNavigationMode = PageNavigationMode.ArbitraryWithTotalPageCount,
            };
            Unloaded += (o, e) => {
                source.Dispose();
            };

            source.FetchPage += (o, e) => {
                e.Result = FetchRowsAsync(e);
            };

            source.GetUniqueValues += (o, e) => {
                if(e.PropertyName == "Priority") {
                    var values = Enum.GetValues(typeof(Priority)).Cast<object>().ToArray();
                    e.Result = Task.FromResult(values);
                } else {
                    throw new InvalidOperationException();
                }
            };

            source.GetTotalSummaries += (o, e) => {
                e.Result = GetTotalSummariesAsync(e);
            };

            grid.ItemsSource = source;
        }

        static async Task<FetchRowsResult> FetchRowsAsync(FetchPageAsyncEventArgs e) {
            IssueSortOrder sortOrder = GetIssueSortOrder(e);
            IssueFilter filter = MakeIssueFilter(e.Filter);

            var issues = await IssuesService.GetIssuesAsync(
                page: e.Skip / e.Take,
                pageSize: e.Take,
                sortOrder: sortOrder,
                filter: filter);

            return new FetchRowsResult(issues, hasMoreRows: issues.Length == e.Take);
        }

        static async Task<object[]> GetTotalSummariesAsync(GetSummariesAsyncEventArgs e) {
            IssueFilter filter = MakeIssueFilter(e.Filter);
            var summaryValues = await IssuesService.GetSummariesAsync(filter);
            return e.Summaries.Select(x => {
                if(x.SummaryType == SummaryType.Count)
                    return (object)summaryValues.Count;
                if(x.SummaryType == SummaryType.Max && x.PropertyName == "Created")
                    return summaryValues.LastCreated;
                throw new InvalidOperationException();
            }).ToArray();
        }

        static IssueSortOrder GetIssueSortOrder(FetchPageAsyncEventArgs e) {
            if(e.SortOrder.Length > 0) {
                var sort = e.SortOrder.Single();
                if(sort.PropertyName == "Created") {
                    if(sort.Direction != ListSortDirection.Descending)
                        throw new InvalidOperationException();
                    return IssueSortOrder.CreatedDescending;
                }
                if(sort.PropertyName == "Votes") {
                    return sort.Direction == ListSortDirection.Ascending
                        ? IssueSortOrder.VotesAscending
                        : IssueSortOrder.VotesDescending;
                }
            }
            return IssueSortOrder.Default;
        }

        static IssueFilter MakeIssueFilter(CriteriaOperator filter) {
            return filter.Match(
                binary: (propertyName, value, type) => {
                    if(propertyName == "Votes" && type == BinaryOperatorType.GreaterOrEqual)
                        return new IssueFilter(minVotes: (int)value);

                    if(propertyName == "Priority" && type == BinaryOperatorType.Equal)
                        return new IssueFilter(priority: (Priority)value);

                    if(propertyName == "Created") {
                        if(type == BinaryOperatorType.GreaterOrEqual)
                            return new IssueFilter(createdFrom: (DateTime)value);
                        if(type == BinaryOperatorType.Less)
                            return new IssueFilter(createdTo: (DateTime)value);
                    }

                    throw new InvalidOperationException();
                },
                and: filters => {
                    return new IssueFilter(
                        createdFrom: filters.Select(x => x.CreatedFrom).SingleOrDefault(x => x != null),
                        createdTo: filters.Select(x => x.CreatedTo).SingleOrDefault(x => x != null),
                        minVotes: filters.Select(x => x.MinVotes).SingleOrDefault(x => x != null),
                        priority: filters.Select(x => x.Priority).SingleOrDefault(x => x != null)
                    );
                },
                @null: default(IssueFilter)
            );
        }
    }
}
