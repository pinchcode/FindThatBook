export interface QueryHypothesis {
  title: string | null;
  author: string | null;
  keywords: string[];
}

export interface BookCandidate {
  title: string;
  author: string;
  allAuthors: string[];
  firstPublishYear: number | null;
  workKey: string;
  workUrl: string;
  coverImageUrl: string | null;
  explanation: string;
}

export interface SearchResponse {
  originalQuery: string;
  parsedQuery: QueryHypothesis | null;
  candidates: BookCandidate[];
  apiWarning: string | null;
}
