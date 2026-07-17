export type CitationPart = { text: string; number?: number };

export function splitCitationMarkers(content: string): CitationPart[] {
  const parts: CitationPart[] = [];
  let plainStart = 0;
  let index = 0;

  const flushProtected = (end: number) => {
    if (index > plainStart) parts.push({ text: content.slice(plainStart, index) });
    parts.push({ text: content.slice(index, end) });
    index = end;
    plainStart = end;
  };

  while (index < content.length) {
    if (content[index] === '`' && !isEscaped(content, index)) {
      flushProtected(skipMatchingBackticks(content, index));
      continue;
    }

    if (content[index] !== '[' || isEscaped(content, index)) { index++; continue; }
    const bracket = findBracket(content, index);
    if (bracket.close < 0) { index++; continue; }
    const image = index > 0 && content[index - 1] === '!';
    const link = bracket.close + 1 < content.length && content[bracket.close + 1] === '(';
    if (bracket.nested || image || link) {
      flushProtected(link ? skipLinkDestination(content, bracket.close) : bracket.close + 1);
      continue;
    }

    const marker = content.slice(index + 1, bracket.close);
    if (/^[1-9][0-9]*$/.test(marker)) {
      const number = Number(marker);
      if (Number.isSafeInteger(number)) {
        if (index > plainStart) parts.push({ text: content.slice(plainStart, index) });
        parts.push({ text: content.slice(index, bracket.close + 1), number });
        index = bracket.close + 1;
        plainStart = index;
        continue;
      }
    }

    index = bracket.close + 1;
  }

  if (plainStart < content.length) parts.push({ text: content.slice(plainStart) });
  return parts;
}

function skipMatchingBackticks(value: string, start: number): number {
  let length = 1;
  while (value[start + length] === '`') length++;
  for (let index = start + length; index < value.length;) {
    if (value[index] !== '`') { index++; continue; }
    let candidate = 1;
    while (value[index + candidate] === '`') candidate++;
    if (candidate === length) return index + length;
    index += candidate;
  }
  return start + length;
}

function findBracket(value: string, start: number): { close: number; nested: boolean } {
  let depth = 1;
  let nested = false;
  for (let index = start + 1; index < value.length; index++) {
    if (value[index] === '[' && !isEscaped(value, index)) { depth++; nested = true; }
    else if (value[index] === ']' && !isEscaped(value, index) && --depth === 0) return { close: index, nested };
  }
  return { close: -1, nested };
}

function skipLinkDestination(value: string, close: number): number {
  if (value[close + 1] !== '(') return close + 1;
  let depth = 1;
  for (let index = close + 2; index < value.length; index++) {
    if (value[index] === '(' && !isEscaped(value, index)) depth++;
    else if (value[index] === ')' && !isEscaped(value, index) && --depth === 0) return index + 1;
  }
  return value.length;
}

function isEscaped(value: string, index: number): boolean {
  let slashCount = 0;
  while (index > 0 && value[--index] === '\\') slashCount++;
  return slashCount % 2 === 1;
}
