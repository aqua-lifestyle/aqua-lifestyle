// NDJSON transport for the local classifier. No signature or authenticity claim.
export default async function* reporter(source) {
  for await (const { type, data } of source) {
    if (type === 'test:pass' || type === 'test:fail') {
      yield JSON.stringify({ type, data: { name: data.name, file: data.file,
        nesting: data.nesting, skip: data.skip, todo: data.todo,
        details: { type: data.details?.type } } }) + '\n';
    } else if (type === 'test:summary' && !data.file) {
      yield JSON.stringify({ type, data: { success: data.success, counts: data.counts } }) + '\n';
    }
  }
}
