import { ProductDetails } from "@/src/components/products/product-details";

type ProductDetailsPageProps = {
  params: Promise<{
    productId: string;
  }>;
};

export default async function ProductDetailsPage({
  params,
}: ProductDetailsPageProps) {
  const { productId } = await params;

  return <ProductDetails productId={Number(productId)} />;
}
