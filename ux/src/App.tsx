import {
  Button,
  Card,
  CardBody,
  Container,
  Flex,
  HStack,
  Heading,
  Image,
  Input,
  InputGroup,
  InputRightElement,
  Text,
} from "@chakra-ui/react";
import { useState } from "react";
import original from "../assets/original.png";
import redacted from "../assets/redacted.png";

function App() {
  const [step, setStep] = useState(0);

  return (
    <Container background="gray.200" rounded="xl" height="100vh" padding="4">
      <Flex
        direction="column"
        justifyContent="flex-start"
        gap="4"
        height="100%"
        overflowY="scroll"
      >
        <Heading>IR-LLM</Heading>
        {step < 1 && (
          <Card width="100%">
            <CardBody>
              <Button
                colorScheme="teal"
                variant="ghost"
                onClick={() => setStep(step + 1)}
              >
                Select Image
              </Button>
            </CardBody>
          </Card>
        )}
        {step >= 1 && (
          <Card width="100%">
            <CardBody>
              <Image src={original} />
            </CardBody>
          </Card>
        )}
        {step >= 2 && (
          <>
            <Card width="max-content">
              <CardBody>
                <Text>I have redacted the image based on your request:</Text>
              </CardBody>
            </Card>
            <Card width="100%">
              <CardBody>
                <Image src={redacted} />
              </CardBody>
            </Card>
          </>
        )}
        {step >= 1 && (
          <>
            <Card width="max-content">
              <CardBody>
                <Text>What{step >= 2 && " else"} do you want to redact?</Text>
              </CardBody>
            </Card>
            <HStack>
              <Button colorScheme="teal" variant="ghost">
                Names
              </Button>
              <Button colorScheme="teal" variant="ghost">
                Address
              </Button>
              <Button colorScheme="teal" variant="ghost">
                Phone Number
              </Button>
            </HStack>
            <InputGroup size="md">
              <Input
                pr="4.5rem"
                placeholder="Or, type your request here"
                background="white"
              />
              <InputRightElement width="4.5rem">
                <Button h="1.75rem" size="sm" onClick={() => setStep(step + 1)}>
                  Send
                </Button>
              </InputRightElement>
            </InputGroup>
          </>
        )}
      </Flex>
    </Container>
  );
}

export default App;
